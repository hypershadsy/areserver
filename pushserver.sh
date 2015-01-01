#!/bin/bash

FILES="Areserver/bin/Debug/Areserver.exe Areserver/bin/Debug/Lidgren.Network.dll"

rsync -avz -e 'ssh -p 2222' --progress ${FILES} Theater@giga.krash.net:/cygdrive/c/Users/Theater/Desktop/jared/areserver
