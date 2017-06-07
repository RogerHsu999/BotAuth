using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace ZimbraBOT
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                await Conversation.SendAsync(activity, () => new Dialogs.RootDialog());
            }
            else
            {
                await HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private async Task<Activity> HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                if (message.MembersAdded.Any(o => o.Id == message.Recipient.Id))
                {
                    if (message.ChannelId == "directline") return null;

                    var RootDialog_Welcome_Message = "您好，我是 **Zimbra** 小秘書。\n\n 您可以透過我來操作您的 Mail or Schedules ";
                    var reply = message.CreateReply(RootDialog_Welcome_Message);
                    var connector = new ConnectorClient(new Uri(message.ServiceUrl));
                    //await connector.Conversations.ReplyToActivityAsync(reply);
                    //Send a (non-reply) message
                    //await connector.Conversations.SendToConversationAsync(reply);

                    var userAccount = new ChannelAccount(message.From.Id);
                    var botAccount = new ChannelAccount(message.Recipient.Id);
                    var replyConversation =
                        await connector.Conversations.CreateDirectConversationAsync(botAccount, userAccount);
                    var replyMsg = Activity.CreateMessageActivity();
                    replyMsg.From = botAccount;
                    replyMsg.Recipient = userAccount;
                    replyMsg.Conversation = new ConversationAccount(id: replyConversation.Id);
                    replyMsg.Text = RootDialog_Welcome_Message;
                    await connector.Conversations.SendToConversationAsync((Activity)replyMsg);
                }
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
                var reply = message.CreateReply();
                reply.Type = ActivityTypes.Ping;
                return reply;
            }
            return null;
        }
    }
}