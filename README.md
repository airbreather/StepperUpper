# StepperUpper
An automated application to ease STEP (and similar) modpack setup.

Note: this documentation is outdated.  I'll get to proper documentation [later](https://github.com/airbreather/StepperUpper/issues/29).

Building
--

Requires Visual Studio 2017 to build.  Community Edition RC is what I use, and it works just fine.

Don't forget to clone recursively (`git clone `**`--recursive`**)  to get [AirBreather.Common](https://github.com/airbreather/AirBreather.Common) into the "External" folder.

Usage
--

It's a command-line application.  Here are the parameters:

- -p / --packDefinitionFiles (required): The .xml files that define the packs.
- -d / --downloadFolder (required): Folder containing downloaded mod files.
- -s / --steamFolder (required): Folder containing "steamapps".
- -o / --outputFolder (required): Folder to create everything in.
- -x / --scorch (optional): Delete contents of output directory if non-empty (otherwise, fail).
- --javaBinFolder (sometimes optional): Folder containing javaw.exe, if used by a pack (required if so, optional otherwise).

Be careful when using "-x".  It deletes **everything** in the directory you specify with "-o", including saves.

See the included "STEP_Core-2.2.9.2.xml" file for an example of the XML file.

Example:
StepperUpper.exe -p "C:\path\to\STEP_Core-2.2.9.2.xml" "C:\path\to\STEP_Extended-2.2.9.2.xml" -d "C:\path\to\Downloads" -s "C:\Games\Steam" -o "C:\Games\STEPDump" --javaBinFolder "C:\Program Files (x86)\Java\jre1.8.0_111\bin"

The "-d" folder is where we'll look for things that you've downloaded, in practice these will mostly be downloaded from Nexus.  If you've already been using Mod Organizer for less automated forays into Skyrim modding, you can reuse many files you've already downloaded by pointing this at the "downloads" subfolder of Mod Organizer, assuming you haven't been deleting them to save space.  *If you've been deleting them after manual installs, well... sorry, but you'll have to re-download for this tool to work with them.*

Requirements
--

Same requirements that [STEP Core 2.2.9.2](http://wiki.step-project.com/STEP:2.2.9.2) has, plus:

1. .NET Framework 4.6 or higher compatible version.
    1. If you have Windows 10, then you definitely have this already.
    2. Otherwise, get 4.6.2 from [here](https://www.microsoft.com/en-us/download/details.aspx?id=53345).
2. 64-bit Windows (otherwise the custom plugin cleaning process might run out of memory).

Note that this will spawn over a hundred concurrent 7-zip processes, which will tend to stress the resources of your CPU, disk I/O, or both.  If you aren't confident that your system can withstand stress (e.g., if you're like me when I was about 15 years old and wasn't careful enough when overclocking), then you might not want to run this in its current state.  *If this is a problem, let me know and I'll see if I can add an option to limit concurrency*.

Features
--

This tool basically works as follows.  Note that most of this behavior is actually defined in the XML file provided.

1. Makes sure all required files are present, between the download and Skyrim install folder (based on length and MD5).
2. Downloads missing files that are allowed to be downloaded automatically (Nexus-hosted files are not allowed, for non-technical reasons).
3. If any files are still missing, provides URLs for where to find them and exits.
4. Runs a cleaning process on the untouched Update.esm, Dawnguard.esm, HearthFires.esm, and Dragonborn.esm files, saving the results to a location where Mod Organizer will pick them up.  This cleaning process is based on what xEdit does when you follow typical plugin cleaning instructions, though the results will not be exactly identical to what xEdit produces because the latter makes other unrelated changes to the plugin files.
5. Extracts third-party archives to the appropriate locations in the output folder, using settings selected by STEP (no need to go through wizards), making further tweaks ("hiding" files and folders, creating INI files, fixing incorrectly named files, etc.) as specified by STEP.
6. Copies (the Wrapper version of) ENBoost into the Skyrim directory, with INI files configured appropriately.
7. Copies a pre-cooked Bashed Patch into a location where Mod Organizer will pick it up.
8. Creates a Mod Organizer profile that:
    1. Tweaks the Bethesda-provided INI files as indicated by STEP.
    2. Orders the mods themselves as indicated by STEP.
    3. Organizes the load order as indicated by LOOT.
    4. Disables merged plugins as indicated by Wrye Bash.
    5. Sets up the Mod Organizer executables for further manual customization as appropriate.
    6. Uses profile-specific saves instead of the user's "My Games" folder.
9. Creates a RunSkyrim.bat file that will launch SKSE using the Mod Organizer profile.

Manual Steps
--

At least at the time of writing, the following tasks are **not** automated:

1. Downloading files from Nexus Mods.  See "Download Mods & Third-Party Tools" on the [Automation Survey STEP Wiki Page](http://wiki.step-project.com/Automation_Survey#Download_Mods_.26_Third-Party_Tools) for why not.
    1. What the tool **does** do is telling you what files you're missing, makes sure you downloaded the exact version that it was tested with, and gives you the URLs that take you right to the download page.
2. Downloading or installing files from Steam.
    1. You must have Skyrim installed with all DLCs in the expected subdirectory of Steam (steamapps\common\Skyrim) before running this, or it will not work at all.
3. Initializing one-time stuff from the Skyrim launcher.
    1. You have to run the Skyrim launcher at least once before using this tool.  It tweaks some registry settings for you.  If you've run Skyrim before on the same system and haven't changed anything like where Steam is installed, you should be all set.
4. Driver-specific tweaks, i.e., anything in [1.E of the STEP Guide](http://wiki.step-project.com/STEP:2.2.9.2#1.E._Display_.26_Video_Card_Settings).
5. DynDOLOD.  StepperUpper extracts what you need as specified by STEP Core and sets up the executables in Mod Organizer, but it leaves "DynDOLOD Resources" disabled and does not actually run the TexGen or DynDOLOD processes.
    1. After running through the automated process, DynDOLOD may be activated by following the recommendations from the [STEP Wiki Page](http://wiki.step-project.com/Dynamic_Distant_Objects_LOD#DynDOLOD_Output_Files), starting at the "Installation/DynDOLOD Output Files" section ("Installation/DynDOLOD Executables" and "Installation/DynDOLOD Resource Files" have already been done for you).
    2. If you're going to add something else on top of STEP Core, you may want to wait to do this until after you've done that.
6. ENBoost memory config.  The tool sets the memory values for ENBoost to "safe" values that may not be ideal for your system.
    1. After running through the automated process, it is **very highly recommended** that you change the VideoMemorySizeMb value in the [MEMORY] group of enblocal.ini (located in steamapps\common\Skyrim under your Steam directory) according to the guidelines on the [STEP Wiki Page](http://wiki.step-project.com/ENBoost#Configure_enblocal.ini).
    2. You may also want to tweak ReservedMemorySizeMb in the same section, depending on your circumstance.

See #19 for work on a post-process automation for STEP Extended, including the list of manual steps intended to be automated.
