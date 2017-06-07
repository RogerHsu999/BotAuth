using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
 

namespace ZimbraBOT.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        
        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
            var messageText = activity?.Text;
            if (messageText == "logout")
            {
                //如果是登出的話，就將已登入的資料清掉
                context.UserData.RemoveValue(ContextConstants.AuthResultKey);
                context.UserData.SetValue(ContextConstants.MagicNumberValidated, "false");
                context.UserData.RemoveValue(ContextConstants.MagicNumberKey);
            }
            
           
            string isMagicNumberValidated = "";
            context.UserData.TryGetValue<string>(ContextConstants.MagicNumberValidated, out isMagicNumberValidated);
            if (isMagicNumberValidated == "true")
            {
                //已登入過，echo 
                await context.PostAsync($"您已經登入，您輸入的是[{messageText}]");
                context.Wait(this.MessageReceivedAsync);
            }
            else
            {
                //沒登入，就要開啟 ZimbraAuthDialog 來處理登入動作
                await context.Forward(new ZimbraAuthDialog("請先登入 Zimbra 系統 "), this.ResumeAfterAuth, activity, CancellationToken.None);
            }

        }

         
        private async Task ResumeAfterAuth(IDialogContext context, IAwaitable<string> result)
        {
            var message = await result;
            await context.PostAsync(message);
            await context.PostAsync("您已登入，如果您要登出的話，請輸入 \"logout\". ");
            context.Wait(MessageReceivedAsync);
        }
    }


    
}