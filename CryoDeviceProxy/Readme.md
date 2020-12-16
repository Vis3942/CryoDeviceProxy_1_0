# CrypServer

Socket server that allows connections between the device and user interface.

## Deployment

Open a command window and change directory to the foloder containing the solution file. 
Then run the following command to generate the code for running on an RHEL instance on AWS EC2 instance or equivalent x64 cloud based system.

```
dotnet publish -c release -f netcoreapp3.1 -r linux-x64 --self-contained=false

```

### Prerequisites

Requires dotnet core 3.1

