# SimplySerial

###### A serial terminal that runs as a Windows console application.
  
  Written by [Edward Wright](mailto:fasteddy@thewrightspace.net) (fasteddy516).

  Available at https://github.com/fasteddy516/SimplySerial

  _SimplySerial is written in C# and requires that .NET Framework 4.6.1 or newer is installed.  There is a pretty good chance it is already installed on any modern Windows operating system, but if not, it can be downloaded from Microsoft at https://dotnet.microsoft.com/download/dotnet-framework._


# Description

  SimplySerial is a basic serial terminal that runs as a Windows console application.  It provides a quick way to connect to - and communicate with - serial devices through the Windows Command Prompt or PowerShell.  

  SimplySerial can be used directly from Command Prompt/PowerShell and should work with most
  devices that appear in Device Manager as "COMxx".  It was, however, written specifically for
  use within a "terminal" window in [Visual Studio Code](https://code.visualstudio.com/https://code.visualstudio.com/) to provide serial communications with devices running CircuitPython.  Most of the testing and development of this application was done with this use case in mind.   


# Installation

  * If it is not already installed, download and install the [.NET Framework 4.6.1 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net461).
  * Download the [latest release](https://github.com/fasteddy516/SimplySerial/releases/latest) of this application.
  * Open the `.zip` archive that you downloaded and move the `ss.exe` file to a location of your choosing.
  * For easy access from the command prompt (if you want to be able to type `ss` from any directory/folder and have it open a serial terminal), you need to add the folder into which you placed `ss.exe` to your `PATH`.  _(For more information on adding folders to the `PATH`, see [this HowToGeek article](https://www.howtogeek.com/118594/how-to-edit-your-system-path-for-easy-command-line-access/))._


# Using SimplySerial

  By default, SimplySerial will attempt to identify and connect to a CircuitPython-capable board at 9600 baud, no parity, 8 data bits and 1 stop bit.  If no known boards are detected, it will default to the first available serial (COM) port.  If you added the `ss.exe` location to your `PATH`, only have one active CircuitPython board (or COM port) on your machine, and that board/port is a device running CircuitPython (or anything that uses those particular port settings), entering the `ss` command while in Command Prompt or PowerShell should be all you have to do to connect.  If you have multiple COM ports, or need to use different communications settings, you will need to use the appropriate command-line arguments listed below.

  `-h, --help` displays a list of valid command-line arguments

  `-l, --list` displays a list of available COM ports  

  `-c, --com` sets the desired COM port (ex. `-c:1` for COM1, `--com:22` for COM22)

  `-b, --baud` sets the baud rate (ex. `-b:9600`, `--baud:115200`)

  `-p, --parity` sets the parity option (ex. `-p:none`, `--parity:even`) 
  
  `-d, --databits` sets the number of data bits to use (ex. `-d:8`, `--databits:7`)

  `-s, --stopbits` sets the number of stop bits to use (ex. `-s:1`, `--stopbits:1.5`)

  `-a, --autoconnect` sets the desired auto-(re)connect behaviour (ex. `a:NONE`, `--autoconnect:ANY`)
  
  `-q, --quiet` prevents any application messages (connection banner, error messages, etc.) from printing out to the console.

If you wanted to connect to a device on COM17 at 115200 baud, you would use the command `ss -c:17 -b:115200`, or if you really enjoy typing `ss --com:17 --baud:115200`.

Once you're connected, you should see messages from the device on COMxx appear on screen, and anything you type into Command Prompt/PowerShell will be sent to the device.  

To disconnect and exit SimplySerial, press `CTRL-X` at any time.


# Auto-(re)connect functionality

  SimplySerial's `autoconnect` option can be used to determine if and how to connect/reconnect to a device.  These options function as follows:
  
  `--autoconnect:ONE` is the default mode of operation.  If a COM port was specified using the `--com` option, SimplySerial will attempt to connect to the specified port, otherwise it will connect to the first available COM port (giving preference to devices known to be CircuitPython-capable).  In either case, the program will wait until the/a COM port is available, and connect to it when it is.  If the device becomes unavailable at any point (because it was disconnected, etc.), SimplySerial will wait until that specific COM port becomes available again, regardless of any other COM ports that may or may not be available.
  
  `--autoconnect:ANY` is similar to `ONE`, except that when the connected port becomes unavailable, SimplySerial will attempt to connect to any other available port.  This option is useful if you only ever have one COM port available at a time, but can be problematic if you have multiple COM ports connected, or if you have a built-in COM port that is always available.
  
  `--autoconnect:NONE` prevents SimplySerial from waiting for devices and automatically re-connecting.
  

# Using SimplySerial in Visual Studio Code (VSCode)

  In a standard installation of VSCode, opening a "terminal" gets you a Command Prompt or PowerShell window embedded in the VSCode interface.  SimplySerial works exactly the same within this embedded window as it does in a normal Command Prompt or PowerShell, which means if you fit the "easy use case scenario" mentioned above (`ss.exe` added to path, single COM port, 9600 baud, etc.), using SimplySerial within VSCode is as easy as opening a terminal window via the menu bar (`Terminal > New Terminal`) or shortcut key, typing `ss` and pressing enter.

  If you want to make things even simpler, or if you need to use a bunch of command-line arguments and don't want to enter them every time (**and you don't use the terminal window in Visual Studio Code for anything else**) you can have VSCode launch SimplySerial directly whenever you open a terminal window by changing the `terminal.integrated.shell.windows` setting to point to `ss.exe` + any arguments you need to add.  This works well, but will prevent you from having multiple VSCode terminal windows open, as only one application can connect to any given serial port at a given time.


# Contributing

  If you have questions, problems, feature requests, etc. please post them to the [Issues section on Github](https://github.com/fasteddy516/SimplySerial/issues).  If you would like to contribute, please let me know.  I have already put some "enhancement requests" in the Github Issues section with some ideas for improvements, most of which were either beyond my limited C#/Windows programming knowledge, or required more time than I had available! 


# Acknowledgements

  The code used to obtain extra details about connected serial devices (VID, PID, etc.) is a modified version of examples posted by Kamil Górski (freakone) at http://blog.gorski.pm/serial-port-details-in-c-sharp and https://github.com/freakone/serial-reader.  Some modifications were made based on this stackoverflow thread: https://stackoverflow.com/questions/11458835/finding-information-about-all-serial-devices-connected-through-usb-in-c-sharp.
