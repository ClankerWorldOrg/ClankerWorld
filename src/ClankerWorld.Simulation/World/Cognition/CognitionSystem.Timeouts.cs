using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static readonly TimeSpan IdentityMomentTimeout = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan ConversationProviderTimeout = TimeSpan.FromSeconds(45);
}
