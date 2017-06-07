using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;
using LineMessagingAPISDK;
using LineMessagingAPISDK.Models;
using Microsoft.Bot.Connector.DirectLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using msdl = Microsoft.Bot.Connector.DirectLine;
using lm = LineMessagingAPISDK.Models;

//https://github.com/kenakamu/line-bot-sdk-csharp/tree/master/LineMessagingAPISDK

namespace ZimbraBOT.Controllers
{
    public class LineMessagesController : ApiController
    {
        public async Task<HttpResponseMessage> Post(HttpRequestMessage request)
        {
            if (!await VaridateSignature(request))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            var contentString = await request.Content.ReadAsStringAsync();
            try
            {
                var activity = JsonConvert.DeserializeObject<lm.Activity>(contentString);
                foreach (var lineEvent in activity.Events)
                {
                    LineMessageHandler handler = new LineMessageHandler(lineEvent);
                    await handler.Initialize();
                    Profile profile = await handler.GetProfile(lineEvent.Source.UserId);
                    switch (lineEvent.Type)
                    {
                        case EventType.Beacon:
                            await handler.HandleBeaconEvent();
                            break;
                        case EventType.Follow:
                            await handler.HandleFollowEvent();
                            break;
                        case EventType.Join:
                            await handler.HandleJoinEvent();
                            break;
                        case EventType.Leave:
                            await handler.HandleLeaveEvent();
                            break;
                        case EventType.Message:
                            Message message = JsonConvert.DeserializeObject<Message>(lineEvent.Message.ToString());
                            switch (message.Type)
                            {
                                case MessageType.Text:
                                    await handler.HandleTextMessage();
                                    break;
                                case MessageType.Audio:
                                case MessageType.Image:
                                case MessageType.Video:
                                    await handler.HandleMediaMessage();
                                    break;
                                case MessageType.Sticker:
                                    await handler.HandleStickerMessage();
                                    break;
                                case MessageType.Location:
                                    await handler.HandleLocationMessage();
                                    break;
                            }
                            break;
                        case EventType.Postback:
                            await handler.HandlePostbackEvent();
                            break;
                        case EventType.Unfollow:
                            await handler.HandleUnfollowEvent();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            

            
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private async Task<bool> VaridateSignature(HttpRequestMessage request)
        {
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebConfigurationManager.AppSettings["LineChannelSecret"]));
            var computeHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(await request.Content.ReadAsStringAsync()));
            var contentHash = Convert.ToBase64String(computeHash);
            var headerHash = Request.Headers.GetValues("X-Line-Signature").First();
            return contentHash == headerHash;
        }
    }

 

    public class LineMessageHandler
    {
        private lm.Event lineEvent;
        private static string directLineSecret = WebConfigurationManager.AppSettings["DirectLineSecret"];
        private LineClient lineClient = new LineClient(WebConfigurationManager.AppSettings["LineChannelAccessToken"]);
        private msdl.DirectLineClient dlClient = new msdl.DirectLineClient(directLineSecret);
        private string _userId; 
        //public string ConversationId
        //{
        //    get
        //    {
        //        var payload = StateSessions.GetUserPayloadByUserId(_userId, "directline");
        //        var conversationId = string.Empty;
        //        if (payload != null)
        //        {
        //            conversationId = (string)payload["conversationId"];
        //        }
                
        //        if (string.IsNullOrWhiteSpace(conversationId))
        //        {
        //            conversationId = dlClient.Conversations.StartConversation().ConversationId;
        //        }
        //        return conversationId;
        //    }
        //}

        private string watermark = ""; // Limit the messages to get from DirectLine
        //private Dictionary<string, object> userParams;
        private string _ConversationId;
        public LineMessageHandler(lm.Event lineEvent)
        {
            this.lineEvent = lineEvent;
            _ConversationId= dlClient.Conversations.StartConversation().ConversationId;
        }

        public async Task Initialize()
        {
            _userId = lineEvent.Source.UserId ?? lineEvent.Source.GroupId ?? lineEvent.Source.RoomId;
             
        }

        public async Task HandleBeaconEvent()
        {
        }

        public async Task HandleFollowEvent()
        {
        }

        public async Task HandleJoinEvent()
        {
        }

        public async Task HandleLeaveEvent()
        {
        }

        public async Task HandlePostbackEvent()
        {
            msdl.Activity sendMessage = new msdl.Activity()
            {
                Type = "message",
                Text = lineEvent.Postback.Data,
                From = new msdl.ChannelAccount(lineEvent.Source.UserId)
            };

            // Send the message, then fetch and reply messages,
            await dlClient.Conversations.PostActivityAsync(_ConversationId, sendMessage);
            await GetAndReplyMessages();
        }

        public async Task HandleUnfollowEvent()
        {
        }

        public async Task<lm.Profile> GetProfile(string mid)
        {
            return await lineClient.GetProfile(mid);
        }

        public async Task HandleTextMessage()
        {
            var textMessage = JsonConvert.DeserializeObject<lm.TextMessage>(lineEvent.Message.ToString());

            msdl.Activity sendMessage = new msdl.Activity()
            {
                Type = "message",
                Text = textMessage.Text,
                From = new msdl.ChannelAccount(lineEvent.Source.UserId)
            };

            // Send the message, then fetch and reply messages,
            try
            {
                await dlClient.Conversations.PostActivityAsync(_ConversationId, sendMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            await GetAndReplyMessages();
        }

        public async Task HandleMediaMessage()
        {
            Message message = JsonConvert.DeserializeObject<Message>(lineEvent.Message.ToString());
            // Get media from Line server.
            var media = await lineClient.GetContent(message.Id);
            await dlClient.Conversations.UploadAsync(_ConversationId, media.Content, lineEvent.Source.UserId, media.ContentType);
            await GetAndReplyMessages();
        }

        public async Task HandleStickerMessage()
        {
            //https://devdocs.line.me/files/sticker_list.pdf
            var stickerMessage = JsonConvert.DeserializeObject<lm.StickerMessage>(lineEvent.Message.ToString());
            var message = new lm.StickerMessage("1", "1");
            await Reply(new List<Message>() { message });
        }

        public async Task HandleLocationMessage()
        {
            var locationMessage = JsonConvert.DeserializeObject<lm.LocationMessage>(lineEvent.Message.ToString());

            msdl.Activity sendMessage = new msdl.Activity()
            {
                Type = "message",
                Text = locationMessage.Title,
                From = new msdl.ChannelAccount(lineEvent.Source.UserId),
                Entities = new List<Entity>()
                {
                    new Entity()
                    {
                        Type = "Place",
                        Properties = JObject.FromObject(new Place(address:locationMessage.Address,
                            geo:new msdl.GeoCoordinates(
                                latitude: locationMessage.Latitude,
                                longitude: locationMessage.Longitude,
                                name: locationMessage.Title),
                            name:locationMessage.Title))
                    }
                }
            };

            // Send the message, then fetch and reply messages,
            await dlClient.Conversations.PostActivityAsync(_ConversationId, sendMessage);
            await GetAndReplyMessages();
        }

        private async Task Reply(List<Message> replyMessages)
        {
            int i = 0;
            try
            {
                await lineClient.ReplyToActivityAsync(lineEvent.CreateReply(
                    messages: replyMessages.Take(5).ToList()));

                if (replyMessages.Count > 5)
                {
                    i = 1;
                    while (replyMessages.Count > i * 5)
                    {
                        await lineClient.PushAsync(lineEvent.CreatePush(
                            messages: replyMessages.Skip(i * 5).Take(5).ToList()));
                        i++;
                    }
                }
            }
            catch
            {
                try
                {
                    while (replyMessages.Count > i * 5)
                    {
                        await lineClient.PushAsync(lineEvent.CreatePush(
                            messages: replyMessages.Skip(i * 5).Take(5).ToList()));
                        i++;
                    }
                }
                catch (Exception ex)
                {
                    // Error when reply/push failed.
                }
            }
        }

        /// <summary>
        /// Get all messages from DirectLine and reply back to Line
        /// </summary>
        private async Task GetAndReplyMessages()
        {
            msdl.ActivitySet result = string.IsNullOrEmpty(watermark) ?
                await dlClient.Conversations.GetActivitiesAsync(_ConversationId) :
                await dlClient.Conversations.GetActivitiesAsync(_ConversationId, watermark);

            //userParams["Watermark"] = 2; //(Int64.Parse(result.Watermark)).ToString();

            foreach (var activity in result.Activities)
            {
                if (activity.From.Id == lineEvent.Source.UserId)
                    continue;

                List<Message> messages = new List<Message>();

                if (activity.Attachments != null && activity.Attachments.Count != 0 && (activity.AttachmentLayout == null || activity.AttachmentLayout == "list"))
                {
                    foreach (var attachment in activity.Attachments)
                    {
                        if (attachment.ContentType.Contains("card.animation"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#animationcard
                            // Use TextMessage for title and use Image message for image. Not really an animation though.
                            AnimationCard card = JsonConvert.DeserializeObject<AnimationCard>(attachment.Content.ToString());
                            messages.Add(new lm.TextMessage($"{card.Title}\r\n{card.Subtitle}\r\n{card.Text}"));
                            foreach (var media in card.Media)
                            {
                                var originalContentUrl = media.Url?.Replace("http://", "https://");
                                var previewImageUrl = card.Image?.Url?.Replace("http://", "https://");
                                messages.Add(new lm.ImageMessage(originalContentUrl, previewImageUrl));
                            }
                        }
                        else if (attachment.ContentType.Contains("card.audio"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#audiocard
                            // Use TextMessage for title and use Audio message for image.
                            AudioCard card = JsonConvert.DeserializeObject<AudioCard>(attachment.Content.ToString());
                            messages.Add(new lm.TextMessage($"{card.Title}\r\n{card.Subtitle}\r\n{card.Text}"));

                            foreach (var media in card.Media)
                            {
                                var originalContentUrl = media.Url?.Replace("http://", "https://");
                                var durationInMilliseconds = 1;

                                messages.Add(new lm.AudioMessage(originalContentUrl, durationInMilliseconds));
                            }
                        }
                        else if (attachment.ContentType.Contains("card.hero") || attachment.ContentType.Contains("card.thumbnail"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#herocard
                            // https://docs.botframework.com/en-us/core-concepts/reference/#thumbnailcard
                            HeroCard hcard = null;

                            if (attachment.ContentType.Contains("card.hero"))
                                hcard = JsonConvert.DeserializeObject<HeroCard>(attachment.Content.ToString());
                            else if (attachment.ContentType.Contains("card.thumbnail"))
                            {
                                ThumbnailCard tCard = JsonConvert.DeserializeObject<ThumbnailCard>(attachment.Content.ToString());
                                hcard = new HeroCard(tCard.Title, tCard.Subtitle, tCard.Text, tCard.Images, tCard.Buttons, null);
                            }

                            lm.ButtonsTemplate buttonsTemplate = new lm.ButtonsTemplate(
                                hcard.Images?.First().Url.Replace("http://", "https://"),
                                hcard.Subtitle == null ? null : hcard.Title,
                                hcard.Subtitle ?? hcard.Text);

                            if (hcard.Buttons != null)
                            {
                                foreach (var button in hcard.Buttons)
                                {
                                    buttonsTemplate.Actions.Add(GetAction(button));
                                }
                            }
                            else
                            {
                                // Action is mandatory, so create from title/subtitle.
                                var actionLabel = hcard.Title?.Length < hcard.Subtitle?.Length ? hcard.Title : hcard.Subtitle;
                                buttonsTemplate.Actions.Add(new lm.PostbackTemplateAction(actionLabel, actionLabel, actionLabel));
                            }

                            messages.Add(new lm.TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("receipt"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#receiptcard
                            // Use TextMessage and Buttons. As LINE doesn't support thumbnail type yet.

                            ReceiptCard card = JsonConvert.DeserializeObject<ReceiptCard>(attachment.Content.ToString());
                            var text = card.Title + "\r\n\r\n";
                            foreach (var fact in card.Facts)
                            {
                                text += $"{fact.Key}:{fact.Value}\r\n";
                            }
                            text += "\r\n";
                            foreach (var item in card.Items)
                            {
                                text += $"{item.Title}\r\nprice:{item.Price},quantity:{item.Quantity}";
                            }

                            messages.Add(new lm.TextMessage(text));

                            lm.ButtonsTemplate buttonsTemplate = new lm.ButtonsTemplate(title: $"total:{card.Total}", text: $"tax:{card.Tax}");
                            foreach (var button in card.Buttons)
                            {
                                buttonsTemplate.Actions.Add(GetAction(button));
                            }

                            messages.Add(new lm.TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("card.signin"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#signincard
                            // Line doesn't support auth button yet, so simply represent link.
                            SigninCard card = JsonConvert.DeserializeObject<SigninCard>(attachment.Content.ToString());

                            lm.ButtonsTemplate buttonsTemplate = new lm.ButtonsTemplate(text: card.Text);
                            foreach (var button in card.Buttons)
                            {
                                buttonsTemplate.Actions.Add(GetAction(button));
                            }
                            messages.Add(new lm.TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("card.video"))
                        {
                            // https://docs.botframework.com/en-us/core-concepts/reference/#videocard
                            // Use Video message for video and buttons for action.

                            VideoCard card = JsonConvert.DeserializeObject<VideoCard>(attachment.Content.ToString());

                            foreach (var media in card.Media)
                            {
                                var originalContentUrl = media?.Url?.Replace("http://", "https://");
                                var previewImageUrl = card.Image?.Url?.Replace("http://", "https://");

                                messages.Add(new lm.VideoMessage(originalContentUrl, previewImageUrl));
                            }

                            lm.ButtonsTemplate buttonsTemplate = new lm.ButtonsTemplate(title: card.Title, text: $"{card.Subtitle}\r\n{card.Text}");
                            foreach (var button in card.Buttons)
                            {
                                buttonsTemplate.Actions.Add(GetAction(button));
                            }
                            messages.Add(new lm.TemplateMessage("Buttons template", buttonsTemplate));
                        }
                        else if (attachment.ContentType.Contains("image"))
                        {
                            var originalContentUrl = attachment.ContentUrl?.Replace("http://", "https://");
                            var previewImageUrl = attachment.ThumbnailUrl?.Replace("http://", "https://");

                            messages.Add(new lm.ImageMessage(originalContentUrl, previewImageUrl));
                        }
                        else if (attachment.ContentType.Contains("audio"))
                        {
                            var originalContentUrl = attachment.ContentUrl?.Replace("http://", "https://");
                            var durationInMilliseconds = 0;

                            messages.Add(new lm.AudioMessage(originalContentUrl, durationInMilliseconds));
                        }
                        else if (attachment.ContentType.Contains("video"))
                        {
                            var originalContentUrl = attachment.ContentUrl?.Replace("http://", "https://");
                            var previewImageUrl = attachment.ThumbnailUrl?.Replace("http://", "https://");

                            messages.Add(new lm.VideoMessage(originalContentUrl, previewImageUrl));
                        }
                    }
                }
                else if (activity.Attachments != null && activity.Attachments.Count != 0 && activity.AttachmentLayout != null)
                {
                    lm.CarouselTemplate carouselTemplate = new lm.CarouselTemplate();

                    foreach (var attachment in activity.Attachments)
                    {
                        HeroCard hcard = null;

                        if (attachment.ContentType == "application/vnd.microsoft.card.hero")
                            hcard = JsonConvert.DeserializeObject<HeroCard>(attachment.Content.ToString());
                        else if (attachment.ContentType == "application/vnd.microsoft.card.thumbnail")
                        {
                            ThumbnailCard tCard = JsonConvert.DeserializeObject<ThumbnailCard>(attachment.Content.ToString());
                            hcard = new HeroCard(tCard.Title, tCard.Subtitle, tCard.Text, tCard.Images, tCard.Buttons, null);
                        }
                        else
                            continue;

                        TemplateColumn tColumn = new TemplateColumn(
                            hcard.Images.FirstOrDefault()?.Url?.Replace("http://", "https://"),
                            hcard.Subtitle == null ? null : hcard.Title,
                            hcard.Subtitle ?? hcard.Title);

                        if (hcard.Buttons != null)
                        {
                            foreach (var button in hcard.Buttons)
                            {
                                tColumn.Actions.Add(GetAction(button));
                            }
                        }
                        else
                        {
                            // Action is mandatory, so create from title/subtitle.
                            var actionLabel = hcard.Title?.Length < hcard.Subtitle?.Length ? hcard.Title : hcard.Subtitle;
                            tColumn.Actions.Add(new lm.PostbackTemplateAction(actionLabel, actionLabel, actionLabel));
                        }

                        carouselTemplate.Columns.Add(tColumn);
                    }

                    messages.Add(new lm.TemplateMessage("Carousel template", carouselTemplate));
                }
                else if (activity.Entities != null && activity.Entities.Count != 0)
                {
                    foreach (var entity in activity.Entities)
                    {
                        switch (entity.Type)
                        {
                            case "Place":
                                Place place = entity.Properties.ToObject<Place>();
                                GeoCoordinates geo = JsonConvert.DeserializeObject<GeoCoordinates>(place.Geo.ToString());
                                messages.Add(new lm.LocationMessage(place.Name, place.Address.ToString(), geo.Latitude, geo.Longitude));
                                break;
                            case "GeoCoordinates":
                                GeoCoordinates geoCoordinates = entity.Properties.ToObject<GeoCoordinates>();
                                messages.Add(new lm.LocationMessage(activity.Text, geoCoordinates.Name, geoCoordinates.Latitude, geoCoordinates.Longitude));
                                break;
                        }
                    }
                }
                else if (activity.ChannelData != null)
                {
                }
                else if (!string.IsNullOrEmpty(activity.Text))
                {
                    if (activity.Text.Contains("\n\n*"))
                    {
                        var lines = activity.Text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                        lm.ButtonsTemplate buttonsTemplate = new lm.ButtonsTemplate(text: lines[0]);

                        foreach (var line in lines.Skip(1))
                        {
                            buttonsTemplate.Actions.Add(new lm.PostbackTemplateAction(line, line.Replace("* ", ""), line.Replace("* ", "")));
                        }

                        messages.Add(new lm.TemplateMessage("Buttons template", buttonsTemplate));
                    }
                    else
                        messages.Add(new lm.TextMessage(activity.Text));
                }

                await Reply(messages);
            }
        }

        /// <summary>
        /// Create TemplateAction from CardAction.
        /// </summary>
        /// <param name="button">CardAction</param>
        /// <returns>TemplateAction</returns>
        private lm.TemplateAction GetAction(CardAction button)
        {
            switch (button.Type)
            {
                case "openUrl":
                case "playAudio":
                case "playVideo":
                case "showImage":
                case "signin":
                case "downloadFile":
                    return new lm.UriTemplateAction(button.Title, button.Value.ToString());
                case "imBack":
                    return new lm.MessageTemplateAction(button.Title, button.Value.ToString());
                case "postBack":
                    return new lm.PostbackTemplateAction(button.Title, button.Value.ToString(), button.Value.ToString());
                default:
                    return null;
            }
        }
    }
}