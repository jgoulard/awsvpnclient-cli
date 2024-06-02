using System.CommandLine;
using System.Reflection;
using ACVC.Core.Metadata;
using ACVC.Core.OpenVpn;
using ACVC.Core.Saml;
using ACVC.Core.Utils;
using Serilog;
using Serilog.Events;

public class AWSVPNClient
{
    private ILogger log;
    private OvpnConnectionManager connectionManager;
    private OvpnConnectionProfileManager profileManager;

    public AWSVPNClient()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Error() // Change to Information to see the logs from ACVC
            .MinimumLevel.Override("AWSVPNClient", LogEventLevel.Information)
            .WriteTo.Console()
            .CreateLogger();
        this.log = Log.ForContext<AWSVPNClient>();

        this.connectionManager = new(
            new OvpnGtkProcessManager(new OvpnManagement()),
            new SamlManager(
                new SamlAcs(new int[] { 35001 }),
                new TestWrapper()
            )
        );
        this.profileManager = new(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AWSVPNClient"
            ),
            null
        );
    }

    public void ListProfiles()
    {
        this.log.Information("Profiles:");
        foreach (OvpnConnectionProfile profile in this.profileManager.GetProfiles())
        {
            this.log.Information($"\t{profile.ProfileName}");
        }
    }

    public void AddProfile(string profileName, string configFile)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            this.log.Error("Profile name is required");
            return;
        }
        if (string.IsNullOrWhiteSpace(configFile))
        {
            this.log.Error("Config file is required");
            return;
        }
        if (!File.Exists(configFile))
        {
            this.log.Error($"Config file not found: {configFile}");
            return;
        }
        this.profileManager.AddProfile(profileName, configFile);
        this.log.Information($"Profile added: {profileName}");
    }

    public void RemoveProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            this.log.Error("Profile name is required");
            return;
        }
        this.profileManager.RemoveProfile(profileName);
        this.log.Information($"Profile removed: {profileName}");
    }

    public async Task Connect(string profileName)
    {
        OvpnConnectionProfile? selectedProfile = null;
        foreach (OvpnConnectionProfile profile in this.profileManager.GetProfiles())
        {
            if (profile.ProfileName == profileName)
            {
                selectedProfile = profile;
                break;
            }
        }

        if (selectedProfile == null)
        {
            this.log.Error($"Profile not found: {profileName}");
            return;
        }

        string ovpnConfigPath = selectedProfile!.OvpnConfigFilePath;
        if (!File.Exists(ovpnConfigPath))
        {
            this.log.Error($"OVPN config file not found: {ovpnConfigPath}");
        }
        else
        {
            this.log.Information($"Connecting to profile: {selectedProfile.ProfileName}");
            Task connection = this.connectionManager.Connect(selectedProfile, null);
            while (!connection.IsCompleted)
            {
                this.log.Debug("Waiting for connection...");
                await Task.Delay(1000);
            }
        }
    }

    public async Task Disconnect()
    {
        this.log.Information("Disconnecting...");
        await this.connectionManager.Disconnect();
    }
}

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CLI for AWS VPN Client");

        var listProfileCommand = new Command("list-profiles", "List all the profiles");
        rootCommand.AddCommand(listProfileCommand);

        var profileNameArgument = new Argument<string>("profileName", "The name of the profile");
        var configFileArgument = new Argument<string>("configFile", "The path to the OpenVPN config file");

        var addProfileCommand = new Command("add-profile", "Add a new profile");
        addProfileCommand.AddArgument(profileNameArgument);
        addProfileCommand.AddArgument(configFileArgument);
        rootCommand.AddCommand(addProfileCommand);

        var removeProfileCommand = new Command("remove-profile", "Remove a profile");
        removeProfileCommand.AddArgument(profileNameArgument);
        rootCommand.AddCommand(removeProfileCommand);

        var connectCommand = new Command("connect", "Connect to the VPN");
        connectCommand.AddArgument(profileNameArgument);
        rootCommand.AddCommand(connectCommand);

        var disconnectCommand = new Command("disconnect", "Disconnect from the VPN");
        rootCommand.AddCommand(disconnectCommand);

        listProfileCommand.SetHandler(() =>
            {
                new AWSVPNClient().ListProfiles();
            }
        );
        addProfileCommand.SetHandler((profileName, configFile) =>
            {
                new AWSVPNClient().AddProfile(profileName, configFile);
            },
            profileNameArgument, configFileArgument
        );
        removeProfileCommand.SetHandler((profileName) =>
            {
                new AWSVPNClient().RemoveProfile(profileName);
            },
            profileNameArgument
        );
        connectCommand.SetHandler(async (profileName) =>
            {
               await new AWSVPNClient().Connect(profileName);
            },
            profileNameArgument
        );
        disconnectCommand.SetHandler(async () =>
            {
                await new AWSVPNClient().Disconnect();
            }
        );
       
        return await rootCommand.InvokeAsync(args);
    }
}