# BeefBot

## Description
This is a Discord Bot that manages a Starcraft 2 ladder. It supports inserting players, reporting wins, renaming and undoing commands.  
The bot is configurable allowing you to specify which roles have access to modify the ladder, the ladder URL, and other settings. It can automatically update player MMR and race, monitor a list of streams and message when one goes live, and dynamically create/remove channels to maintain 1 empty at all times. <br />
<br />
This bot is intended to be used as a companion to a website that will display the actual ladder. That website can use the BeefApi exposed by this bot to retrieve the current ladder. By default this will be hosted at http://127.0.0.1:5000/beef-ladder (See **Setting up BeefApiConfig.json** for configuration options). The webserver can be notified when the ladder changes by connecting a TCP socket to l27.0.0.1:5002 (by default) and listening for changes. See **Setting up BeefApiConfig.json** for more details.

## Reporting Issues
Write up issues on the github issue tracker at:
https://github.com/kirby561/Beef/issues

Please include as much information as you can, especially the following:
1. What happened and, if applicable, what should have happened instead (Ex: "The bot crashed with this message", "The ladder was updated incorrectly, it should have displayed this instead...")
2. Can you reproduce the issue? With what steps? If not, what were you doing when it occurred? The more information the better.
3. Include any screenshots of the issue that would be helpful in showing the problem.
4. Include the output of the bot, such as errors and stack traces if applicable.
5. The version of BeefBot you are running (Use the ".beef version" command to get this).
6. Anything else that could be helpful to reproduce or understand the issue.

## Build and Run
1. Open Beef.sln in Visual Studio and press build. You will need at least Visual Studio 2022 Community (https://visualstudio.microsoft.com/vs/community/).
2. Verify you have created your configuration files (See **Configuring BeefBot**)
3. After configuring, you can run from Visual Studio or by running Beef.exe.

## Configuring BeefBot
BeefBot has 2 configuration files that need to be setup before you can run: **Config.json** and **BeefApiConfig.json**.
Both config files have examples to show what needs to be configured named **Config.json.example** and **BeefApiConfig.json.example**, respectively. You can start by removing the ".example" extension on each file and then configuring the required settings. Both config files need to be in the same directory as *Beef.exe*. Most settings have reasonable defaults. The ones that must be changed have a \* suffix to indicate that.
At a minimum, you will need to set **DiscordBotToken**, **LeaderRoles**, and **BeefLadderLink**. You can set **MmrReaderConfig** and **TwitchConfig** to **null** if you don't want those features. If you don't have a separate webserver to display the ladder, you can set BeefLadderLink to point to http://localhost:5000/beef-ladder to display just the JSON representation of it (or a DNS name that points to that location).

### Setting up Config.json
**Version** - This should be set to the version of the config file being used. The example file will default to the latest version so for first-time setup, you can leave this alone. If you update BeefBot, change this to the latest version again when you incorporate new settings. <br />
<br />
**DiscordBotToken**\* -  The secret token for your Discord Bot. To get this, you will need a Discord Bot setup on your Discord account. Create a Discord app at https://discord.com/developers/applications, create a bot and copy/paste the secret token in the Bot menu here. A good guide on how to do this is available here: https://discordnet.dev/guides/getting_started/first-bot.html <br />
<br />
BeefBot will need the following permissions: <br />
- General Permissions / Manage Channels (For the dynamic channel feature) <br />
- Text Permissions / Send Messages <br />
- Text Permissions / Send Messages in Threads <br />
- Text Permissions / Read Message History <br />
- Voice Permissions / Connect (For the dynamic channels feature) <br />

**BotPrefix** - The prefix to use for each command. For example "." for *.beef* or "!" for *!beef*
<br />
**LeaderRoles** - An array of Discord role names that have admin access to the bot.
<br />
**DynamicChannels** - An array of channels that should be *dynamic* or *null* to disable the feature. *dynamic channels* are created or removed such that there is always exactly 1 empty channel with that name. For example, if there is one "2v2 Teams" channel and a user joins it, a new channel called "2v2 Teams" will be created with the same permissions. If the user then leaves the initial channel leaving 2 empty "2v2 Teams" channels, one of them will be deleted so there will be exactly 1 empty "2v2 Teams" channel.
<br />
**BeefLadderLink** - A URL to the webpage that will display the ladder. Note that a JSON representation of the ladder can be retrieved at the link specified in the *BeefApiConfig.json* file (See the *BeefApiConfig.json* section for details).
<br />
<br />
**MmrReaderConfig** - Sets up automatic MMR/Race reading for each player on the ladder if they are linked to a Battle.net account (see the *beef link* command). Note you will need to setup a battle.net application for this at develop.battle.net to get an ID/Secret since this bot uses the battle.net API to retrieve MMR data. Set to *null* to disable this feature.
- **Version** - Always 1 for now.
- **MsPerRead** - The number of milliseconds between updating MMR of all linked players. Note this takes a while and there are rate limits on the API so only change this if you know what you are doing.
- **DataDirectory** - The directory to store temporary files in. "." is the executable directory.
- **ClientId**\* - The client ID of your Battle.net application (Create this at https://develop.battle.net)
- **ClientSecret**\* - The client secret of your app (get this from the app created at https://develop.battle.net)
<br />

**TwitchConfig** - Configures the Twitch API for the "Stream Monitoring" feature (see the *beef monitor* command). Set to *null* to disable this feature.
- **Version** - Always 1 for now.
- **MsPerPoll** - How often to check if any streams have gone live in milliseconds. Note that the API has rate limits so don't change this unless you know what you are doing.
- **GoLiveChannel**\* - This is a channel name to send the "go live" message to when the stream goes live.
- **ClientId**\* - You need to create a Twitch App to use this feature. Create an application at https://dev.twitch.tv/console/apps/create and place the app ID here. This will identify the app to use to connect to the Twitch API.
- **ClientSecret**\* - After creating your app at the link above, put the client secret here. This will define the credentials used to connect to the Twitch API.

### Setting up BeefApiConfig.json
**Kestrel.Endpoints.Http.Url** - Sets the HTTP URL and port to host the current ladder at. The ladder can then be accessed at the "/beef-ladder" endpoint (For example: http://localhost/beef-ladder)
<br />
**Kestrel.Endpoints.Https.Url** - Sets the HTTPS URL and port to host the current ladder at. This section can be omitted for HTTP only.
<br />
**LadderChangedEventPort** - The port to broadcast ladder changed events on. Clients can connect a TCP socket to this port to listen for ladder changed events. Change notifications are sent as mesages according to the following protocol: <br />
    **[Length][Message]** <br />
    Where [Length] is a 4 byte integer in Big-Endian byte order and Message is a UTF-8 encoded JSON string that is [Length] bytes long. <br />
For example: <br />
```
 00, 00, 00, 20, 7B, 20, 22, 4D, 65, 73, 73, 61, 67, 65, 22, 3A, 20, 22, 4F, 6E, 4C, 61, 64, 64, 65, 72, 43, 68, 61, 6E, 67, 65, 64, 22, 20, 7D
|--------------| |----------------------------------------------------------------------------------------------------------------------------|
 4 byte Length                                              [Length] bytes of a UTF8 encoded message String
(32 in this case)

The message string will be a JSON string in the following form:
	{ "Message": "OnLadderChanged" }
```

## Contributing
1. Write an issue or pickup one in the github issue tracker.
2. Make sure your issue is opened to you.
3. Implement your change following the coding conventions in this readme.
4. Submit a pull request for review.
> Pull requests should generally contain a single commit based off the head of the target branch.
> For special circumstances, multiple commits  and/or merge commits will be allowed. There are two cases this may be necessary:
> 	a. A long running feature branch with many commits is being merged in.
> 	b. There are many renamed files and it's easier to review the commit if the renames are separate from the content changes.
>   The commits should be prefixed with "Issue#:" and have a good description of the change. For example:
>
>		Issue2: Fixed a thing by implementing the fix in this cool way. The root cause was some insightful thing and this fixes it because of this reason.
>
5. Iterate on feedback as needed.
6. The issue should be closed with the commit (s) that implemented the change and the testing that was done.

## Coding Conventions
Follow mainstream C# coding conventions at https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions 
with the following exceptions:
1. Private/Protected member variables should be prefixed with "_" (for example: _someMemberVariable). This makes them stick out in diff tools and prevents shadowing.
2. Compact bracing is preferred rather than a brace being on its own line. For example:
>	if (...) { <br />
>	  ... <br />
>	} else { <br />
>	  ... <br />
>	} <br />
