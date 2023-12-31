Links your Twitch channel commands to TF2 console commands using the Rcon port.
* Launch
* enter your channel/username ID
* link to twitch (First time it will launch authorization sequence, then save a token for future linking)
* set an Rcon port and password and Launch TF2 (or use pazer's tf2_bot_detector and find its logs folder for configuration to automatically share its randomly-generated port and password)
* enjoy!

Create/Edit the TF2SpectatorCommands.tsv file to use custom stuff. e.g.:<br/>
<pre>
!command&lt;tab&gt;console command with {0} and {1} substitutions from user id and command arg&lt;tab&gt;help text when chat types !help command
!com2|aliasCom2&lt;tab&gt;console command&lt;tab&gt;help text for both com2 and its alias
redeem command title|!chatCommand&lt;tab&gt;redeemed/commanded by user "{0}" with message {1}
redeem with a title that changes sometimes|ababba18-d1ec-44ca-9e30-89303812a601&lt;tab&gt;redeemed command with {0} user and {1} args by ID.
</pre>
Title-based redeems are easier, but if you want to change the title you'll have to change the config file unless you use the reward id as an alias.
One way to get that is "inspect" an edit button of the item in the rewards editor...
![image](https://github.com/id-rotatcepS/TF2Spectator/assets/66532903/2244fcb6-b593-46b8-9882-33f231967699)

and find the value of data-reward-id.

Note, if your channel isn't affiliate (or partner) the Redeems won't even try to work.
