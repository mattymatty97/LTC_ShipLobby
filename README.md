LobbyControl
============
[![GitHub Release](https://img.shields.io/github/v/release/mattymatty97/LTC_LobbyControl?display_name=release&logo=github&logoColor=white)](https://github.com/mattymatty97/LTC_LobbyControl/releases/latest)
[![GitHub Pre-Release](https://img.shields.io/github/v/release/mattymatty97/LTC_LobbyControl?include_prereleases&display_name=release&logo=github&logoColor=white&label=preview)](https://github.com/mattymatty97/LTC_LobbyControl/releases)  
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/mattymatty/LobbyControl?style=flat&logo=thunderstore&logoColor=white&label=thunderstore)](https://thunderstore.io/c/lethal-company/p/mattymatty/LobbyControl/)

### **!!v49 compatibility has been dropped with v2.4.0, if you still play v49 please downgrade to v2.3.6!!**

A collection of Patches for Lethal Company with Quality of life changes for the Host.

**This mod is 100% Vanilla Compatible** and does not change any of the vanilla gameplay.
only **the host** needs this mod installed

Lobby Control has split:
------
some of the features that are not about hosting have been moved to [Matty's Fixes](https://thunderstore.io/c/lethal-company/p/mattymatty/Matty_Fixes/)

Patches:
--------
- ### Steam Lobby ( all controls from terminal ):
  - allow to **re-open** the lobby while the Ship is **in Orbit** ( Late Joining )
  - **change the visibility** of the lobby  
  (public, Invite-Only, Friends-Only)
  - **rename** the lobby
- ### Vanilla Saving ( all controls from terminal ):
  - **toggle autosave** for the current SaveFile  
  ( no need to quit before Orbit to reset a run )
  - **force save** the lobby  
  ( optionally with a alternative filename backup )
  - **hotload** of the lobby  
  ( reload a previous/different savefile without having to quit the lobby )
- ### ItemLimit
  - **removed** limit on amount of items that can be saved  
  ( vanilla = 45 )
  - **removed** limit to amount of scrap that can be synchronized  
  ( vanilla = 250 )
- ### InvisibleManFix
  - **fix for late joining player being invisible if the previous owner of the body disconnected while dead**
- ### ItemSyncFix
  - prevent clients with **extra inventory** from generating unpickable items
  - fix **shotguns disappearing** if client reloads while de-synced
- ### JoinQueue:
  - process incoming connections as a queue to prevent two clients connecting at the same time
- ### LogSpam:
  - fix the causes of some spammy error messages
    - stop **CalculatePolygonPath** when enemies die
- ### Others:
  - Prevent host crashes when an extra player tries to join when the lobby is already full but the last player has not finished joining 
  - Fix wrong names in radarMap when a booster is enabled

Terminal Command:
-----------------

#### Syntax: lobby [command] (option)  
[option]  means required  
(option)  means optional

#### Sub-Commnads:
- status        : prints the current lobby status
- open          : open the lobby
- close         : close the lobby
- private       : set lobby to Invite Only
- friend        : set lobby to Friends Only
- public        : set lobby to Public
- rename \[name] : change the name of the lobby
- autosave      : toggle the autosave state
- save (name)   : forcefully save the lobby
- load (name)   : re-load the lobby from SaveFile

Differences to [ShipLobby](https://thunderstore.io/c/lethal-company/p/tinyhoot/ShipLobby/)
------------------------
This mod started as a Fork of ShipLobby
and instead of always reopening the lobby it allows the host to decide if and when with terminal commands

Differences to [MoreItems](https://thunderstore.io/c/lethal-company/p/Drakorle/MoreItems/)
------------------------
MoreItems simply sets the max amount of items to the arbitrary value of 999.  
This mod instead removes the limit entirely, additionally it also allows you to sync the scrap value of all those items

Installation
------------

- Install [BepInEx](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/)
- Unzip this mod into your `Lethal Company/BepInEx/plugins` folder

Or use the mod manager to handle the installing for you.

Terminal Code by
-----------
@felix_argyle_nya on discord  
<@675902479245770810>  
[GitHub](https://github.com/ZehsTeam) | [Thunderstore](https://thunderstore.io/c/lethal-company/p/Zehs/)

Icon by
-----------
@pedro9006 on discord  
<@464801863741734913>
