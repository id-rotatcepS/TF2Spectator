Links your Twitch channel commands to TF2 console commands using the Rcon port.  Also includes a Bot Kicker.
* Launch
* enter your channel/username ID
* link to twitch (First time it will launch authorization sequence, then save a token for future linking)
* set an Rcon port and password and configure TF2 (in launch configuration and autoexec.cfg)
* enjoy!

What you get:
* *!help* or *!commands* lists commands that start with ! for any twitch user
* commands run a TF2 console command in your game, can include user's message, and can chat back the result
* commands from a channel points redeem title or id
* commands can translate between words for/from chat and values in commands
* commands can include a random word from a list
* includes special translated commands: "crosshair aim color..." (words and numeric colors) "tf2 class selection" (numeric and named classes) "kick a bot..." "list bots"
* Bot Kicker can watch your game lobby for marked bots and auto-kick them when it sees them; tells chat the bot names from the lobby with *!bots*
* Bot Kicker can suggest a bot by name similar to common bots or to a chat suggestion like *!bot Frank The Bot*
* bonus: responds with answers to basic math problems like "2*3+1.4"

Create/Edit the TF2Spectator Commands to use custom stuff. e.g.:<br/>

<table>
<tr><td>!command</td><td>console command with {0} and {1} substitutions from user id and command arg</td><td>help text when chat types !help command</td></tr>
<tr><td>!com2|aliasCom2</td><td>console command</td><td>help text for both com2 and its alias</td></tr>

<tr><td>redeem command title|!chatCommand</td><td>redeemed/commanded by user "{0}" with message {1}</td></tr>
<tr><td>redeem with a title that changes sometimes|ababba18-d1ec-44ca-9e30-89303812a601</td><td>redeemed command with {0} user and {1} args by ID.</td></tr>

<tr><td>!fancy</td><td>console command with {1|off:0|on:1} arg remapping and {cl_class} command substitutions</td><td>help text</td><td>post-command chat output with {0} username {1} command output and {cl_class} additional variable/command output</td></tr>

<tr><td>!hitSound</td><td>tf_dingalingaling_effect</td><td>What hit sound is in use?</td><td>Current hit sound is {1|0:0 (Default)|1:1 (Electro)|2:2 (Notes)|3:3 (Percussion)|4:4 (Retro)|5:5 (Space)|6:6 (Beepo)|7:7 (Vortex)|8:8 (Squasher)}</td></tr>
</table>

*Note, chat output happens on rcon response but this responds just before any "wait" command in the sequence, so only includes output up until that command.*  
In other words "echo one;echo two;wait 10;echo three" will populate chat's {1} with "one&lt;newline&gt;two"

---
Title-based redeems are easier, but if you want to change the title you'll have to change the config file unless you use the reward id as an alias.
One way to get that is "inspect" an edit button of the item in the rewards editor...
![image](https://github.com/id-rotatcepS/TF2Spectator/assets/66532903/2244fcb6-b593-46b8-9882-33f231967699)

and find the value of data-reward-id.

Note, if your channel isn't affiliate (or partner) the Redeems won't even try to work.
