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
ababba18-d1ec-44ca-9e30-89303812a601&lt;tab&gt;redeemed command with {0} user and {1} args
</pre>
Until title-based redeems are working, you have to use the reward id as a command name or alias.  
One way to get that is "inspect" an edit button of the item in the rewards editor...
![image](https://github.com/id-rotatcepS/TF2Spectator/assets/66532903/2244fcb6-b593-46b8-9882-33f231967699)

and find the value of data-reward-id
