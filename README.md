Links Twitch channel commands to TF2 console commands using the Rcon port.
* Launch
* enter your channel/username ID
* link to twitch (First time it will launch authorization sequence, then save a token for future linking)
* set an Rcon port and password and Launch TF2 (or launch tf2_bot_detector, check its logs, and copy its randomly-generated port and password to share the port with both apps)
* enjoy!

Create/Edit the TF2SpectatorCommands.tsv file to use custom stuff. e.g.:<br/>
<pre>
!command&lt;tab&gt;console command with {0} substitution from command arg&lt;tab&gt;help text when chat types !help command
!com2|aliasCom2&lt;tab&gt;console command&lt;tab&gt;help text for both com2 and its alias
</pre>
