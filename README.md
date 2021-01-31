# What is this?
This is a simple tool to aid in solving the Treasure Hunter Monolith puzzle minigame in Danganronpa V3 (and in particular, obtaining the 'Monolithic Achievement' trophy). Its main use is in brute-forcing a solution that will hopefully earn you enough points for that coveted S rank.

# How do I install it?
There's a standalone Windows binary in the Releases section; just extract wherever and run TreasureFinder.exe. Alternatively you can build from source in Unity 2020.1 or later (though I'd definitely recommend making a standalone build rather than running in Unity itself; it's significantly faster that way).

# What does it do?
It has three main modes:
* Edit mode lets you manually fill out the board, and add the locations of hidden fish/Monokubs (henceforth just 'treasure').
* Solve mode attempts to find a solution (i.e. a series of moves) that clears as much of the board as possible, prioritising clearing treasures.
* Playback mode lets you step through the solution the tool has found, or you can use it to try working out solutions yourself by hand.

# How do I use it?
The basic sequence of steps I used for the trophy was something like this:
1) Start THM up, take a photo/screenshot of the board, and then minimise/pause DRV3 so the in-game timer isn't running.
2) Copy the initial layout into the tool using 'Edit' mode. (Note: The tool starts in Edit mode when you first open it.)
3) Run the tool's 'Solve' mode for a while. This will attempt to find a solution that clears as many spaces as possible, but at this point it doesn't know where the hidden treasure is, so whatever it comes up with probably isn't going to be good enough.
4) Switch the tool into 'Playback' mode, unpause DRV3, and start performing in-game the solution the tool has generated until you discover a treasure. Once you do, pause the game again.
5) Switch the tool back into 'Edit Current' mode and add the location of that treasure.
6) Run the 'Solve' mode again. This will now attempt to find a new solution that prioritises clearing that treasure, starting from the point you've got to so far.
7) Once you've got a new solution that (ideally) clears that treasure, go back into 'Playback' mode and start performing it in-game until you find another treasure.
8) Repeat steps 5-7 a few times. Hopefully, you'll discover the hidden Monokubs early enough that the tool can find a solution that actually clears them and earns you enough points. If not, go back to step 1 and try again with a new layout.

This approach isn't 100% reliable (sometimes you just won't find the Monokubs early enough; sometimes the tool just won't find a good enough solution in the time you give it; sometimes a solution might not even exist), but it got me the trophy on my second attempt. You can also try varying it up a bit (e.g. try digging out a few random tiles manually to find the Monokubs before you copy the board into the tool for the first time), but your general approach is going to be "dig a bit, find some treasure, solve, repeat".

# What are the controls?
## Edit Mode
* Use the arrow keys to move the cursor around.
* Use the 1-4 number keys to add a tile, or 0 to remove a tile. This moves the cursor automatically on to the next space.
* Hold Shift and move the cursor to add a rectangular Monokub region, or Ctrl to add a fish (i.e. move the cursor to one corner of where you think the treasure is, hold Shift/Ctrl, move cursor to the other corner, release). Monokubs are highlighted in green, fish in red.
* Tap Shift/Ctrl while the cursor is over a treasure region to remove it again. There's no editing of regions once you've placed them; if you mess one up, just remove it and redraw it.
* When you're done editing, click the 'Solve' button to enter Solve mode.

## Solve Mode
* While you're in this mode, the tool will keep trying out solutions and will remember the best one it's found so far. You can see how many attempts it's tried and its estimated score in the right-hand panel.
* Use the 'Playback' button to enter Playback mode, or 'Edit' to go back to Edit mode. You can switch back and forth between modes without losing your current solution, as long as you don't make any changes in Edit mode.

## Playback Mode
* Use PageUp/PageDown to step back/forwards through the best solution the tool has generated. Home/End jump to the start/end states.
* Use the arrow keys to move the cursor.
* Press Delete to dig out a group of matching tile, changing all tiles around it (i.e. the same thing that digging out a tile in-game does). You can use this if you want to try out solutions manually in the tool (and then use PageUp to undo your edits). Making manual changes halfway through a solution will discard the rest of the solution after that point.
* Use the 'Edit' button to go back to Edit mode, resetting the board to its original layout (i.e. what it was when you were last in Edit mode).
* Use the 'Edit Current' button to go back to Edit mode, keeping the board in its current layout. Use this one when you're working through a solution in-game and you discover a treasure. (Note: Doing this will discard the board's original layout and any solution generated so far, and leave you with just 'what the board currently looks like'.)

# Miscellaneous questions
## Why does my predicted score in Solve mode sometimes go down?
Internally, the tool treats empty tiles as being worth less if the tiles next to them aren't also empty (under the assumption that if there's a treasure it doesn't know about, it's a bit more likely to clear contiguous regions this way and so might uncover them on its own). The score it shows you is the actual score the solution should get you in-game, so it's possible that its predicted in-game score might go down if it thinks that its latest solution has a better chance of finding extra treasure. TBH, for the purposes of the trophy, what really matters is how many of the treasures you can uncover rather than how many individual tiles you clear.

## How long should I leave it in Solve mode?
There's not really a good answer to this. The longer you leave it going, the better a solution it's going to find, but there's very definite diminishing returns and you'll notice pretty early that it seems to have settled on a reasonable solution and isn't finding anything better. I found letting it run for a couple of minutes each time (probably a few hundred thousand attempts) was good enough.

## Why does the tool do X / why doesn't it do X?
The tool does pretty much exactly what I needed it to do to get the trophy, and nothing more. I'm releasing it in this state because it was Good Enough for me, but there's definitely plenty of ways it could be improved.

## Can I make changes to it?
Feel free. I made this in Unity 2020.1, but I imagine it can be made to work in any vaguely recent Unity version without too much pain. You'll need to reimport the TextMeshPro assets, but Unity ought to prompt you for that when you open the SampleScene.unity scene. I make no guarantees as to the quality of my code.
