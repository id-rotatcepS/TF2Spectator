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

!fancy&lt;tab&gt;console command with {1|off:0|on:1} arg remapping and {cl_class} command substitutions&lt;tab&gt;help text&lt;tab&gt;post-command chat output with {0} username {1} command output and {cl_class} additional variable/command output

!hitSound&lt;tab&gt;tf_dingalingaling_effect&lt;tab&gt;What hit sound is in use?&lt;tab&gt;Current hit sound is {1|0:0 (Default)|1:1 (Electro)|2:2 (Notes)|3:3 (Percussion)|4:4 (Retro)|5:5 (Space)|6:6 (Beepo)|7:7 (Vortex)|8:8 (Squasher)}
</pre>
Note, chat output happens on rcon response but this responds just before any "wait" command in the sequence, so only includes output up until that command.  
In other words "echo one;echo two;wait 10;echo three" will populate chat's {1} with "one&lt;newline&gt;two"

Title-based redeems are easier, but if you want to change the title you'll have to change the config file unless you use the reward id as an alias.
One way to get that is "inspect" an edit button of the item in the rewards editor...
![image](https://github.com/id-rotatcepS/TF2Spectator/assets/66532903/2244fcb6-b593-46b8-9882-33f231967699)

and find the value of data-reward-id.

Note, if your channel isn't affiliate (or partner) the Redeems won't even try to work.
