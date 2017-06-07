# BotAuth
參考 AuthBot，演示 Bot 如何讓使用者登入內部系統取得驗證碼

## Microsoft Bot Framework FAQ - 驗證內部系統(AuthBot)

一般來說除了使用公用的服務外，在使用系統之前都需要登入系統。
透過 BOT 來使用私有的服務也是需要登入的哦!
我們可以參考「[Build BOT with Authentication (Microsoft Bot Framework)](https://blogs.msdn.microsoft.com/tsmatsuz/2016/09/06/microsoft-bot-framework-bot-with-authentication-and-signin-login/)」及「[MicrosoftDX/AuthBot](https://github.com/MicrosoftDX/AuthBot)」。
來實作 BOT 登入我們內部系統的方式。

運作的過程如下圖，
![authentication steps](http://i1155.photobucket.com/albums/p551/tsmatsuz/20160906_Bot_Auth02_zpsnb28vmp8.jpg)


### 1.判斷使用者沒有登入系統，就導到處理登入的 ZimbraAuthDialog ( RootDialog -> ZimbraAuthDialog )

```
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
```

### 2.在 ZimbraAuthDialog 中如果沒有登入，就產生一個 SigninCard 來讓使用者點選開啟 Browser 
這裡我們呼叫 IMessageActivity.ToConversationReference() 來取得 Conversation 資訊，讓它可以串在 QueryString 之中。

```
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
```

### 3.在登入的頁面，成功登入後，產生驗證碼，並將相關資訊存到 BOT 的 UserData 之中，並將需要輸入驗證碼的訊息回給 Channel(請注意:如果用 direct line 是收不到的哦! 筆者還在努力中 ...)。 如下，


```
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
```

### 4.使用者將登面頁面中的驗證碼輸入到 Channel 之中，處理登入的 ZimbraAuthDialog 就會接手判斷驗證碼是否正確，如果不正確就請使用者重新輸入，如果正確就結束 ZimbraAuthDialog ，回到 RootDialog 。

```
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
```
