# SimplySerial

###### A serial terminal that runs as a Windows console application.
  
Written by [Edward Wright](mailto:fasteddy@thewrightspace.net) (fasteddy516).

Available at https://github.com/fasteddy516/SimplySerial

# Description

SimplySerial is a basic serial terminal that runs as a Windows console application.  It provides a quick way to connect to - and communicate with - serial devices through Command Prompt or PowerShell.  SimplySerial can be used directly from Command Prompt/PowerShell and should work with most devices that appear in Device Manager as "COMx".  It was, however, written specifically for use within a "terminal" window in [Visual Studio Code](https://code.visualstudio.com/) to provide serial communications with devices running [CircuitPython](https://circuitpython.org/).  Most of the testing and development of this application was done with this use case in mind.  

# A Quick Note For CircuitPython Users

If your primary interest in SimplySerial is for programming CircuitPython devices in Visual Studio Code, _I encourage you to check out Joe DeVivo's excellent VSCode extension_ in the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=joedevivo.vscode-circuitpython) or [On GitHub](https://github.com/joedevivo/vscode-circuitpython).  His extension has tons of awesome features that go well beyond the basic 'serial terminal' functionality provided by SimplySerial.  That being said, SimplySerial is still a handy little tool for quickly connecting to serial devices in a Command Prompt/PowerShell, for use in VSCode for non-CircuitPython devices, or for those who prefer its simplicity over the full-featured CircuitPython extension.

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

The standalone version can also be installed with [scoop](https://scoop.sh/).

```powershell
> scoop bucket add extras
> scoop install simplyserial
```

# Using SimplySerial

For CircuitPython users, type `ss` in a Command Prompt, PowerShell or VSCode Terminal Window and press `enter`.  That's it!

By default, SimplySerial will attempt to identify and connect to a CircuitPython-capable board at 115200 baud, no parity, 8 data bits and 1 stop bit.  If no known boards are detected, it will default to the first available serial (COM) port at 9600 baud.  If there are no COM ports available, it will wait until one shows up, then connect to it. 

Once you're connected, you should see messages from the device on COMx appear on screen, and anything you type into Command Prompt/PowerShell will be sent to the device.  CircuitPython users can access the REPL using `CTRL-C` and exit the REPL using `CTRL-D`.

You can exit SimplySerial any time by pressing `CTRL-X`.  

If you have multiple COM ports, multiple CircuitPython devices connected, or need to use different communications settings, you will need to use the appropriate command-line arguments listed below:

  `-h, --help` displays a list of valid command-line arguments

  `-v, --version` displays version and installation information

  `-l, --list` displays a list of available COM ports  

  `-c, --com` sets the desired COM port (ex. `-c:1` for COM1, `--com:22` for COM22)

  `-b, --baud` sets the baud rate (ex. `-b:9600`, `--baud:115200`)

  `-p, --parity` sets the parity option (ex. `-p:none`, `--parity:even`) 
  
  `-d, --databits` sets the number of data bits to use (ex. `-d:8`, `--databits:7`)

  `-s, --stopbits` sets the number of stop bits to use (ex. `-s:1`, `--stopbits:1.5`)

  `-a, --autoconnect` sets the desired auto-(re)connect behaviour (ex. `a:NONE`, `--autoconnect:ANY`)
  
  `-l, --log` logs all output to the specified file  (ex. `-l:ss.log`, `-log:"C:\Users\My Name\my log.txt"`)

  `--logmode` instructs SimplySerial to either `APPEND` to an existing log file, or `OVERWRITE` an existing log file.  In either case, if the specified log file does not exist, it will be created.  If neither option is specified, `OVERWRITE` is assumed.  (ex. `--logmode:APPEND`)

  `-q, --quiet` prevents any application messages (connection banner, error messages, etc.) from printing out to the console

  `-f, --forcenewline` replaces carriage returns with linefeeds in received data

  `-e, --encoding` sets the encoding to use when outputting to the terminal and log files.  Defaults to `UTF8`, can also be set to `ASCII` (the default in SimplySerial versions prior to 0.8.0) or `RAW`. In `RAW` mode, all non-printable characters are displayed as `[xx]` where `xx` is the hexadecimal byte value of the character.

  `-noc --noclear` don't clear the terminal screen on connection

  `-nos --nostatus` block status/title updates generated by virtual terminal sequences (such as the CircuitPython status bar introduced in CP version 8.0.0)

  `-ec --echo` enable or disable printing typed characters

  `-tx --tx_newline` newline chars sent on carriage return (ex. `-tx:CRLF`, `-tx:custom=CustomString`, `--tx_newline:LF`)

  `-i, --input` ut configuration file, with newline separated configuration options. eg: `c:COM1`. Note that the prefix `-` and `--` shall be omitted.

If you wanted to connect to a device on COM17 at 115200 baud, you would use the command `ss -c:17 -b:115200`, or if you really enjoy typing `ss --com:17 --baud:115200`.


# Auto-(re)connect functionality

  SimplySerial's `autoconnect` option can be used to determine if and how to connect/reconnect to a device.  These options function as follows:
  
  `--autoconnect:ONE` is the default mode of operation.  If a COM port was specified using the `--com` option, SimplySerial will attempt to connect to the specified port, otherwise it will connect to the first available COM port (giving preference to devices known to be CircuitPython-capable).  In either case, the program will wait until the/a COM port is available, and connect to it when it is.  If the device becomes unavailable at any point (because it was disconnected, etc.), SimplySerial will wait until that specific COM port becomes available again, regardless of any other COM ports that may or may not be available.
  
  `--autoconnect:ANY` is similar to `ONE`, except that when the connected port becomes unavailable, SimplySerial will attempt to connect to any other available port.  This option is useful if you only ever have one COM port available at a time, but can be problematic if you have multiple COM ports connected, or if you have a built-in COM port that is always available.
  
  `--autoconnect:NONE` prevents SimplySerial from waiting for devices and automatically re-connecting.
  

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