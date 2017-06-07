using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using Microsoft.Bot.Builder.ConnectorEx;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZimbraBOT.Modules;

namespace ZimbraBOT.Dialogs
{
    [Serializable]
    public class ZimbraAuthDialog:IDialog<string>
    {
        protected string _prompt { get; }

        public ZimbraAuthDialog(string prompt)
        {
            _prompt = prompt;
        }

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var msg = await argument;
            AuthResult authResult;
            //先判斷個人資料中是否包含 登入驗證結果
            if (context.UserData.TryGetValue(ContextConstants.AuthResultKey, out authResult))
            {
                try
                {
                    string validated;
                    context.UserData.TryGetValue<string>(ContextConstants.MagicNumberValidated, out validated);
                    if (validated == "true")
                    {
                        //已輸入登入頁面的驗證碼，可以結束本 Dialog
                        context.Done($"謝謝 {authResult.UserName}. 您已登入系統. ");
                    }
                    else
                    {
                        //尚未登入頁面的驗證碼
                        var magicNumber = 0;
                        if (context.UserData.TryGetValue<int>(ContextConstants.MagicNumberKey, out magicNumber))
                        {
                            if (msg.Text == null)
                            {
                                await context.PostAsync(
                                    $"請輸入登入完成後的{ContextConstants.MagicNumberLength}位數字驗證碼");
                                context.Wait(this.MessageReceivedAsync);
                            }
                            else
                            {
                                if (msg.Text.Length >= ContextConstants.MagicNumberLength && magicNumber.ToString() == msg.Text.Substring(0, ContextConstants.MagicNumberLength))
                                {
                                    //驗證成功，將資訊寫到 UserData 之中，並結束本 Dialog
                                    context.UserData.SetValue<string>(ContextConstants.MagicNumberValidated, "true");
                                    context.Done($"謝謝 {authResult.UserName}. 您已登入系統. ");
                                }
                                else
                                {
                                    //驗證失敗，重新輸入一次
                                    await context.PostAsync($"驗證碼錯誤，請重新輸入登入完成後的{ContextConstants.MagicNumberLength}位數字驗證碼!");
                                    context.Wait(this.MessageReceivedAsync);

                                    //也可以讓user重新登入
                                    //context.UserData.RemoveValue(ContextConstants.AuthResultKey);
                                    //context.UserData.SetValue<string>(ContextConstants.MagicNumberValidated, "false");
                                    //context.UserData.RemoveValue(ContextConstants.MagicNumberKey);
                                    //await this.LogIn(context, msg);
                                }
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    context.UserData.RemoveValue(ContextConstants.AuthResultKey);
                    context.UserData.SetValue(ContextConstants.MagicNumberValidated, "false");
                    context.UserData.RemoveValue(ContextConstants.MagicNumberKey);
                    context.Done($"I'm sorry but something went wrong while authenticating.");
                }
            }
            else
            {
                await this.LogIn(context, msg);
            }

        }


        private async Task LogIn(IDialogContext context, IMessageActivity msg)
        {
            try
            {
                //記錄 userId 及 Conversation 的資訊
                var stateObject = new
                {
                    UserId = context.Activity.From.Id,
                    ConversationReference = msg.ToConversationReference()
                };
                //將資訊 Serialize 後，放在 QueryString 上面傳
                string state = Uri.EscapeDataString(JsonConvert.SerializeObject(stateObject));
                var hostUrl = WebConfigurationManager.AppSettings["hostUrl"];
                var loginPath = WebConfigurationManager.AppSettings["loginPath"];
                string authenticationUrl = $"{hostUrl}{loginPath}?state={state}";
                await PromptToLogin(context, msg, authenticationUrl);
                context.Wait(this.MessageReceivedAsync);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }


        protected virtual Task PromptToLogin(IDialogContext context, IMessageActivity msg, string authenticationUrl)
        {
            Attachment plAttachment = null;
            switch (msg.ChannelId)
            {
                default:
                {
                    SigninCard plCard = new SigninCard(this._prompt, GetCardActions(authenticationUrl, "signin"));
                    plAttachment = plCard.ToAttachment();
                    break;
                }
            }
            IMessageActivity response = context.MakeMessage();
            response.Recipient = msg.From;
            response.Type = "message";

            response.Attachments = new List<Attachment>();
            response.Attachments.Add(plAttachment);
            return context.PostAsync(response);
        }

        private List<CardAction> GetCardActions(string authenticationUrl, string actionType)
        {
            List<CardAction> cardButtons = new List<CardAction>();
            CardAction plButton = new CardAction()
            {
                Value = authenticationUrl,
                Type = actionType,
                Title = "Authentication Required"
            };
            cardButtons.Add(plButton);
            return cardButtons;
        }

    }
}