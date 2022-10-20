$version = $args[0]
$desc = $args[1]

dotnet publish -c Release -r win-x64 /p:InformationalVersion="$version" /p:Description="$desc" -o "buildoutput/" PiWriteBack/