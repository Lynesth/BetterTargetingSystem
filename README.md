# Better Targeting System

Better Targeting System is a plugin that tries to improve the way Tab targeting works.  
It uses cones of different sizes depending on distance to identify which targets can be acquired.

Here's what it does when you press the `Cycle Targets` keybind:

- It first tries to target an enemy in front of your character in the direction your camera is facing (as explained above).
- If no enemies are in one of those cones in front of you, it will then target a very close enemy (< 5y).
- If there are no enemies nearby, it will target an enemy you are currently in combat with, using the enemy list \*.
- If there are still no enemies, it will then just default to any available target visible on your screen.

The plugin will not target enemies you cannot interact with, such as those in another party's levequest / treasure hunt and will also try its best to not change the order in which enemies are cycled through (can probably be improved).

It also adds an extra keybind to target the lowest (absolute) health enemy as well as a keybind to target the "best" enemy for targeted aoes.
You can use /bts to configure the keybinds used by this plugin as well as the angles and ranges of the cones and circle used to cycle between targets.

**This plugin is disabled in PvP.**

Do not hesitate to give feedback/suggestion and submit bug reports on the Github repository.

\* Yes, TAB will now target those huge bosses without having to reorient the camera to get the center of their model in view  
(DPS players might not understand why this is an issue).
