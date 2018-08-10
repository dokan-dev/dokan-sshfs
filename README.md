# Dokan SSHFS
[![Build status](https://ci.appveyor.com/api/projects/status/yjj6m84g69l1mh06?svg=true)](https://ci.appveyor.com/project/Liryna/dokan-sshfs)

## What is Dokan SSHFS
Dokan SSHFS is a program that mounts remote file systems using SSH.

## Licensing
Dokan SSHFS is distributed under a version of the "MIT License",
which is a BSD-like license. See the 'license.txt' file for details.

## Environment
This program requires following programs.
- Dokan library version 1.1.0.2000 (https://github.com/dokan-dev/dokany/releases/tag/v1.1.0.2000)
- Dokan.NET (including)
- SharpSSH (including)
- Microsoft .NET Framework 4.0
- Microsoft Visual C++ 2005 SP1 Redistributable Package
(http://www.microsoft.com/downloads/details.aspx?FamilyID=200b2fd9-ae1a-4a14-984d-389c36f85647)

SharpSSH http://www.tamirgal.com/home/dev.aspx?Item=SharpSsh
I changed SharpSSH to deal with multibyte characters.

## Download
You can download last version on our CI appveyor [here](https://ci.appveyor.com/api/projects/liryna/dokan-sshfs/artifacts/dokan-sshfs.zip). Don't forget to install dokany dependency (see Environment) !

## How to build
 - Open the directory: `dokan-sshfs\DokanSSHFS`
 - Open `DokanSSHFS.sln` in visual studio
 - Click build and this time choose `Build Solution`
 - After it's done, you can find the executable in `dokan-sshfs\DokanSSHFS\bin\Release`

## How to use
Just run DokanSSHFS.exe in the build folder.
DokanSSHFS supports only OpenSSH key format.

see also https://github.com/dokan-dev/dokany/wiki

## Uninstallation
You can use Add/Remove programs in Control Panel to uninstall Dokan.
DokanSSHFS saves settings under "Documents and Settings\UserName\
Application Data\DokanSSHFS" and uninstaller doesn't remove them.

