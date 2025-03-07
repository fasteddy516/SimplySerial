# SimplySerial

  ###### A serial terminal that runs as a Windows console application.
  
  Written by [Edward Wright](mailto:fasteddy@thewrightspace.net) (fasteddy516).

  Available at https://github.com/fasteddy516/SimplySerial


# Description

  SimplySerial is a basic serial terminal that runs as a Windows console application.  It provides a quick way to connect to - and communicate with - serial devices through Command Prompt or PowerShell.  SimplySerial can be used directly from Command Prompt/PowerShell and should work with most devices that appear in Device Manager as "COMx".  It was, however, written specifically for use within a "terminal" window in [Visual Studio Code](https://code.visualstudio.com/) to provide serial communications with devices running [CircuitPython](https://circuitpython.org/).  Most of the testing and development of this application was done with this use case in mind.  


# Requirements

  * Windows 10 or 11 _(Version [0.6.0](https://github.com/fasteddy516/SimplySerial/releases/tag/v0.6.0) and older will also run on Windows 7, 8 and 8.1)_

  * .NET Framework 4.8 or newer

  _The required version of .NET framework is already included in supported Windows versions.  If it is missing on your machine, you can download and install it from Microsoft at https://dotnet.microsoft.com/download/dotnet-framework._


# Installation

  Download the [latest release](https://github.com/fasteddy516/SimplySerial/releases/latest) of this application in one of three formats:

  `SimplySerial_x.x.x_user_setup.msi` is a windows installer package that puts everything where it needs to go and adds the location of the SimplySerial executable to your `PATH` environment variable, which makes it easily accessible from Command Prompt, PowerShell and Visual Studio Code.  Installation is per-user, and does not require Administrative rights to install.  **This is the preferred installation method,** _and works well with the "user setup" version of VSCode_.

  `SimplySerial_x.x.x_system_setup.msi` is similar to `user_setup.msi` except that the installation is system-wide (for all users), and **requires administrative rights to install.**  _This version will work with both the "user setup" and "system setup" versions of VSCode_.

  **_If you are unsure which version of VSCode you have installed, load it up and go to `Help > About` - beside the version number it will say either `user` or `system` setup._**

  **_The installer versions are unsigned, and may trigger a "Windows Defender SmartScreen" warning. To install you have to press "More Info" followed by "Run Anyway"._**

  `SimplySerial_x.x.x_standalone.zip` is a standard compressed archive containing SimplySerial's program files and some documentation.  You can unzip it wherever you like, and add that location to your `PATH` or not.  **Advanced users may prefer this format/process.**

  The standalone version can also be installed with [scoop](https://scoop.sh/).  Assuming you have scoop installed, you can install SimplySerial using the following commands:

  ```powershell
  > scoop bucket add extras
  > scoop install simplyserial
  ```

  After SimplySerial is installed through scoop, you can update it when new versions become available using the following commands:

  ```powershell
  > scoop update
  > scoop update simplyserial
  ```


# Using SimplySerial

  For CircuitPython users, type `ss` in a Command Prompt, PowerShell or VSCode Terminal Window and press `enter`.  That's it!

  By default, SimplySerial will attempt to identify and connect to a CircuitPython-capable board at 115200 baud, no parity, 8 data bits and 1 stop bit.  If no known boards are detected, it will default to the first available serial (COM) port at 9600 baud.  If there are no COM ports available, it will wait until one shows up, then connect to it. 

  Once you're connected, you should see messages from the device on COMx appear on screen, and anything you type into Command Prompt/PowerShell will be sent to the device.  CircuitPython users can access the REPL using `CTRL-C` and exit the REPL using `CTRL-D`.

  You can exit SimplySerial any time by pressing `CTRL-X`.  

  If you have multiple COM ports, multiple CircuitPython devices connected, or need to use different communications settings, you will need to use the appropriate command-line arguments listed below:

  `-help` displays a list of valid command-line arguments

  `-version` displays version and installation information

  `-list` displays a list of available COM ports.

  `-list:all` displays a list of all available COM ports including those that have been excluded using device filters.

  `-list:settings` displays a list of all command-line arguments that have been loaded from configuration files.

  `-list:boards` displays a list of all recognized serial devices.

  `-list:filters` displays a list of all device filters.

  `-com` sets the desired COM port (ex. `-c:1` for COM1, `-com:22` for COM22)

  `-baud` sets the baud rate (ex. `-b:9600`, `-baud:115200`)

  `-parity` sets the parity option (ex. `-p:none`, `-parity:even`) 
  
  `-databits` sets the number of data bits to use (ex. `-d:8`, `-databits:7`)

  `-stopbits` sets the number of stop bits to use (ex. `-s:1`, `-stopbits:1.5`)

  `-autoconnect` sets the desired auto-(re)connect behaviour (ex. `-a:NONE`, `-autoconnect:ANY`)
  
  `-log` logs all output to the specified file  (ex. `-l:ss.log`, `-l:"C:\Users\My Name\my log.txt"`)
            
  `-logmode` instructs SimplySerial to either `APPEND` to an existing log file, or `OVERWRITE` an existing log file.  In either case, if the specified log file does not exist, it will be created.  If neither option is specified, `OVERWRITE` is assumed.  (ex. `-logmode:APPEND`)

  `-quiet` prevents any application messages (connection banner, error messages, etc.) from printing out to the console

  `-forcenewline` replaces carriage returns with linefeeds in received data. (ex. `-forcenewline:on`)

  `-encoding` sets the encoding to use when outputting to the terminal and log files.  Defaults to `UTF8`, can also be set to `ASCII` (the default in SimplySerial versions prior to 0.8.0) or `RAW`. In `RAW` mode, all non-printable characters are displayed as `[xx]` where `xx` is the hexadecimal byte value of the character.

  `-clearscreen` enable/disable clearing of the terminal screen on connection (ex. `-clearscreen:off`)

  `-status` enable/disable status/title updates generated by virtual terminal sequences (such as the CircuitPython status bar introduced in CP version 8.0.0) (ex. `-status:off`)

  `-title` sets the console window title.  Surround with quotation marks if your title has spaces.  (ex. `-title:"My SimplySerial Window"`)

  `-bulksend` enables or disables bulk send mode (sending all characters typed/pasted at once) (ex. `-bulksend:on`)

  `-config` loads a set of command-line arguments from the specified file.  (One command per line.) (ex. `-config:commands.cfg`)

  `-echo` enables or disables printing typed characters locally (ex. `-echo:on`)

  `-exitkey` specifies the key to use along with CTRL for exiting the program (default is 'X'). (ex. `-exitkey:Z` means you now quit SimplySerial by pressing `CTRL-Z`)

  -`txonenter` determines what character(s) will be sent when the enter key is pressed.  Valid options are `CR`, `LF`, `CRLF`, `CUSTOM="Custom String"` and `BYTES="custom sequence of bytes"`.  Byte sequences must be expressed as 2-digit hexadecimal values with or without leading `0x` and separated by spaces or not.  (ex. `-txonenter:BYTES="0x31 0x32 0x33 0x0D"` or `-txonenter:BYTES="3132330D"`, etc.)

  `-updateboards` searches for - and optionally installs - updates to the `boards.json` data file used for serial device recognition.

  If you wanted to connect to a device on COM17 at 115200 baud, you would use the command `ss -c:17 -b:115200`, or if you really enjoy typing `ss --com:17 --baud:115200`.

  _Note that SimplySerial is very forgiving when it comes to command-line arguments.  You can start each argument with a single dash `-`, double-dash `--` or no dashes at all.  You can shorten commands and parameters - `ss --list:settings` and `ss l:s` are both valid and do exactly the same thing.  In cases where commands start with the same letter(s), specific commands have been given priority, i.e. `ss -l` will get you `ss -list`, not `ss -log`._


# Auto-(re)connect functionality

  SimplySerial's `autoconnect` option can be used to determine if and how to connect/reconnect to a device.  These options function as follows:
  
  `-autoconnect:ONE` is the default mode of operation.  If a COM port was specified using the `-com` option, SimplySerial will attempt to connect to the specified port, otherwise it will connect to the first available COM port (giving preference to devices known to be CircuitPython-capable).  In either case, the program will wait until the/a COM port is available, and connect to it when it is.  If the device becomes unavailable at any point (because it was disconnected, etc.), SimplySerial will wait until that specific COM port becomes available again, regardless of any other COM ports that may or may not be available.
  
  `-autoconnect:ANY` is similar to `ONE`, except that when the connected port becomes unavailable, SimplySerial will attempt to connect to any other available port.  This option is useful if you only ever have one COM port available at a time, but can be problematic if you have multiple COM ports connected, or if you have a built-in COM port that is always available.
  
  `-autoconnect:NONE` prevents SimplySerial from waiting for devices and automatically re-connecting.


# Customizing Settings and Behaviour

  SimplySerial allows you to modify its default behaviour through global settings, project settings and - if specified on the command-line - user settings.

  ### Global Settings

  When SimplySerial starts, it looks for a file called `settings.cfg` in its application folder (the same location as the `ss.exe` program file.)  If the file exists, command-line arguments are read from the file and applied.  If the contents of `settings.cfg` were as follows:
  ```
  encoding:ASCII
  bulksend:ON
  clearscreen:OFF
  ```
  then every time SimplySerial starts up, the specified `encoding`, `bulksend` and `clearscreen` options will be applied automatically without having to enter them on the command-line.  All command-line options are valid, although commands that force SimplySerial to exit (i.e. `-list`, `-help`, `-version`, etc.) will be ignored.  As on the command-line, you can prefix each line with single, double or no dashes - whatever you prefer.

  ### Project Settings

  SimplySerial will also look for a `settings.cfg` file in a `.simplyserial` subfolder of your current working folder.  For example, if you are working with CircuitPython you can create a `.simplyserial` folder on the `CIRCUITPY` drive, place a `settings.cfg` file in that folder, and if you run `ss.exe` from the root of your `CIRCUITPY` drive it will automatically pull in the settings you've specified here.  
  
  ### User Settings

  You can also tell SimplySerial to load settings from a specific file of your choosing by using the `-config` command-line option.  (ex. `ss.exe -config:my_custom_config.cfg`).

  ### Altogether Now!

  You can use all, none or any combination of the above configuration file options.  SimplySerial will load and apply Global settings first, then Project settings, then User settings, and finally settings entered on the command-line itself.  If the same command is present in multiple files, the last one to be applied takes precedence.  


# Customizing Device Recognition

  SimplySerial uses the `boards.json` file located in the same folder as `ss.exe` to apply useful manufacturer/model names to serial devices.  (ex. you see `Raspberry Pi Pico 2 W` instead of `VID:239A PID:8162`.)  You can add your own devices by placing a `custom_boards.json` file in the SimplySerial application folder using the same format as the existing `boards.json` file.  Note that devices in `custom_boards.json` with the same VID and PID as devices in the default `boards.json` file will take precedence.

  You can also place a `custom_boards.json` file in the `.simplyserial` Project Settings subfolder (_see above_), and it will be applied when SimplySerial is started from your project's root folder.


# Filtering out unwanted COM ports

  Sometimes there are COM devices that you just want SimplySerial to ignore - bluetooth COM ports, weird COM ports built into asset management systems on laptops, old-school 9-pin serial ports built into some desktop PCs, etc.  You can tell SimplySerial to ignore these ports by creating a `filters.json` file in the application folder (where `ss.exe` is located), or you can create project-level device filters by placing `filters.json` in the `.simplyserial` project folder.  The format is as follows:

  ```json
  [
    {
        "Type": "INCLUDE",
        "Match": "STRICT",
        "Port": "*",
        "VID": "239A",
        "PID": "*",
        "Description": "*",
        "Device": "*"
    },
    {
        "Type": "EXCLUDE",
        "Match": "LOOSE",
        "Description": "bluetooth",
    },
        {
        "Type": "EXCLUDE",
        "Match": "CIRCUITPYTHON"
    }
  ]
  ```


  `Type` is required, and can either be `"INCLUDE"` or `"EXCLUDE"`.  If you use `INCLUDE` filters, *only devices matching the filters you've defined will be used by SimplySerial.*  If you don't define any `INCLUDE` filters, then all ports are included by default unless they match an `EXCLUDE` filter.  If you combine both types, only those devices that *do* match the `INCLUDE` filters and *don't* match the `EXCLUDE` filters will be used.

  `Match` is required, and can either be `"STRICT"` or `"LOOSE"`.  `STRICT` means any parameters you've defined must exactly match for the filter to be applied.  `LOOSE` means any parameters you've defined must be contained within the corresponding value of the COM device for the filter to match.  `LOOSE` comparisons are also case-insensive.

  `Port`, `VID`, `PID`, `Description` and `Device` all correspond to the identically named columns in the table printed out with the `-list` command.  Set parameters that you don't want to use in your filter to `"*"`, or just leave the parameter out altogether.

  The example above can be broken down as follows:
  - The first filter ensures that SimplySerial will only use COM devices with a VID of `239A`.
  - The second filter will exclude any device that contains the word `bluetooth` in its description.
  - The third filter is a special case - setting `Match` to `CIRCUITPYTHON` in an `EXCLUDE` filter tells SimplySerial to stop prioritizing CircuitPython devices over other COM devices.


# Using SimplySerial in Visual Studio Code (VSCode)

  In a standard installation of VSCode, opening a "terminal" gets you a Command Prompt or PowerShell window embedded in the VSCode interface.  SimplySerial works exactly the same within this embedded window as it does in a normal Command Prompt or PowerShell, which means using SimplySerial within VSCode is as easy as opening a terminal window via the menu bar (`Terminal > New Terminal`) or shortcut key, typing `ss` and pressing enter.

  If you want to make things even simpler, or if you need to use a bunch of command-line arguments and don't want to enter them every time (**and you don't use the terminal window in Visual Studio Code for anything else**) you can have VSCode launch SimplySerial directly whenever you open a terminal window by changing the `terminal.integrated.shell.windows` setting to point to `ss.exe` + any arguments you need to add.  This works well, but will prevent you from having multiple VSCode terminal windows open, as only one application can connect to any given serial port at a given time.


# Using SimplySerial with Windows Terminal

  [Windows Terminal](https://docs.microsoft.com/en-us/windows/terminal/) is a tabbed alternative to the command shell that Microsoft has developed as an open source project.  It is easy to setup SimplySerial as a new terminal profile; you just need to create a new profile in the settings GUI and specify the ss command line.  If you have problems, make sure that the SimplySerial executable is in your system path.

  If you're directly editing the settings.json, the profile section will look like the code below, but with your specific command-line parameters.

    {
        "commandline": "ss -com:4 -baud:115200",
        "name": "COM4"
    }


# Contributing

  If you have questions, problems, feature requests, etc. please post them to the [Issues section on GitHub](https://github.com/fasteddy516/SimplySerial/issues).  If you would like to contribute, please let me know.  I have already put some "enhancement requests" in the GitHub Issues section with some ideas for improvements, most of which were either beyond my limited C#/Windows programming knowledge, or required more time than I had available! 


# Acknowledgements

  The code used to obtain extra details about connected serial devices (VID, PID, etc.) is a modified version of [serial-reader](https://github.com/freakone/serial-reader) and its [associated examples](http://blog.gorski.pm/serial-port-details-in-c-sharp) by Kamil Górski (@freakone).  Some modifications were made based on [this stackoverflow thread](https://stackoverflow.com/questions/11458835/finding-information-about-all-serial-devices-connected-through-usb-in-c-sharp).

  The code implemented in v0.6.0 to enable virtual terminal processing is based on Tamás Deme's (@tomzorz) gist about [Enabling VT100 terminal emulation in the current console window](https://gist.github.com/tomzorz/6142d69852f831fb5393654c90a1f22e).

  The improved detection of CircuitPython boards in version 0.7.0 is based on Simon Mourier's answer on [this stackoverflow thread](https://stackoverflow.com/questions/69362886/get-devpkey-device-busreporteddevicedesc-from-win32-pnpentity-in-c-sharp) regarding the retrieval of a device's hardware bus description through WMI, with some pointers taken from Adafruit's [adafruit_board_toolkit](https://github.com/adafruit/Adafruit_Board_Toolkit/blob/main/adafruit_board_toolkit).


