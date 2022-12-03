# BudgetBot

How to setup on Raspberry Pi
1. SSH into Pi
2. Download the correct sdk for your raspberry pi OS (either arm32 or arm64) from [microsoft's download page](https://dotnet.microsoft.com/en-us/download/dotnet)
```
2. cd ~
3. mkdir downloads
4. cd downloads
5. wget https://download.visualstudio.microsoft.com/download/pr/67ca3f83-3769-4cd8-882a-27ab0c191784/bf631a0229827de92f5c026055218cc0/dotnet-sdk-6.0.403-linux-arm64.tar.gz
```
3. Install the dotnet SDK
```
sudo mkdir /usr/share/dotnet
sudo tar -zxf dotnet-sdk-6.0.403-linux-x64.tar.gz -C /usr/share/dotnet
export DOTNET_ROOT=/usr/share/dotnet
export PATH=$PATH:/usr/share/dotnet
```
4. Add the two above export statements to the end of the ~/.bashrc file using nano ~./bashrc. You should now be able to run dotnet --info
5. Clone this repo (if you are using the lite version of the OS then you need to install git)
```
mkdir github
cd github
sudo apt install git
git clone https://github.com/connorivy/BudgetBot.git
cd BudgetBot
```
6. copy the config file and replace the placeholder info with your real information
```
cp config.json-example config.json
nano config.json
```
7. build the project and copy the config.json file to the correct folder
```
dotnet build
cp config.json bin/Debug/net6.0/config.json
```
8. create database and copy it to local location
```
dotnet tool install --global dotnet-ef
export PATH="$PATH:/home/pi/.dotnet/tools"
dotnet-ef migrations add InitialCreate
dotnet ef database update
cp BudgetBot.db bin/Debug/net6.0/BudgetBot.db
```
9. Publish and run!
```
 dotnet publish -c Release -p:UseAppHost=false
 cp config.json bin/Release/net6.0/config.json
 dotnet bin/Release/net6.0/BudgetBot.dll
```

If you want to leave the program running even when the ssh is disconnected, use [screen](https://raspberrypi.stackexchange.com/questions/8085/will-terminating-an-ssh-connection-also-terminate-any-program-running).
