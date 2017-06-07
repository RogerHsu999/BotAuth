using System;
using System.Security.Cryptography;
using Autofac;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using Microsoft.Rest;
using Newtonsoft.Json;
using ZimbraBOT.Modules;


namespace ZimbraBOT.OAuth
{
    public partial class Login : System.Web.UI.Page
    {
        private static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
        private static readonly uint MaxWriteAttempts = 5;


        protected void Page_Load(object sender, EventArgs e)
        {
            phInfo.Visible = false;
            if (!Page.IsPostBack)
            {
                Session["state"] = Request.QueryString["state"];
            }
        }

        /// <summary>
        /// 產生亂數
        /// </summary>
        /// <returns></returns>
        private int GenerateRandomNumber()
        {
            int number = 0;
            byte[] randomNumber = new byte[1];
            do
            {
                rngCsp.GetBytes(randomNumber);
                var digit = randomNumber[0] % 10;
                number = number * 10 + digit;
            } while (number.ToString().Length < ContextConstants.MagicNumberLength);
            return number;

        }


        protected async void btnLogin_OnClick(object sender, EventArgs e)
        {
            try
            {
                phInfo.Visible = true;
                var state = JsonConvert.DeserializeObject<State>((string)Session["state"]);
                var token = "fakeToken"; 
                if (!string.IsNullOrWhiteSpace(token))
                {
                    int magicNumber = GenerateRandomNumber();
                    // Create the message that is send to conversation to resume the login flow
                    var message = state.ConversationReference.GetPostToUserMessage();
                    using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, message))
                    {
                        var authResult = new AuthResult();
                        authResult.AccessToken = token;
                        authResult.UserName = txtUserId.Text;
                        IStateClient sc = scope.Resolve<IStateClient>();
                        bool writeSuccessful = false;
                        uint writeAttempts = 0;
                        //將資料存回 botState 之中
                        while (!writeSuccessful && writeAttempts++ < MaxWriteAttempts)
                        {
                            try
                            {
                                var userData = sc.BotState.GetUserData(message.ChannelId, message.Recipient.Id);
                                userData.SetProperty(ContextConstants.AuthResultKey, authResult);
                                userData.SetProperty(ContextConstants.MagicNumberKey, magicNumber);
                                userData.SetProperty(ContextConstants.MagicNumberValidated, "false");
                                sc.BotState.SetUserData(message.ChannelId, message.Recipient.Id, userData);
                                writeSuccessful = true;
                            }
                            catch (HttpOperationException)
                            {
                                writeSuccessful = false;
                            }
                        }

                        if (!writeSuccessful)
                        {
                            message.Text = String.Empty; // fail the login process if we can't write UserData
                            await Conversation.ResumeAsync(state.ConversationReference, message);
                            txtAlertMsg.Text = "無法登入，請再試一次，謝謝您!";
                             
                        }
                        else
                        {
                            await Conversation.ResumeAsync(state.ConversationReference, message);
                            txtAlertMsg.Text = $"請將以下的{ContextConstants.MagicNumberLength}個數字驗證碼輸入到IM之中，以完成登入程序，謝謝您!<br/> <h1>{magicNumber}</h1>";
                        }
                    }
                }
                else
                {
                    // login fail
                    txtAlertMsg.Text = "登入失敗! 請重新輸入! ";
                }
            }
            catch
            {
                txtAlertMsg.Text = "不是從 IM 進來的!";
            }
            

        }
    }


}