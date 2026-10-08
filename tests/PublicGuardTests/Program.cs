using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using GekiDrive;
using MU3.Battle;
using MU3.Client;
using MU3.User;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
var engine = new GameEngine(); engine.applyResultToUserData(new SessionResult());
Check(engine.Saved == 1, "Fixture result method does not execute before protection"); engine.Saved = 0;
bool Prefix(string name, object[] arguments)
{ return (bool)typeof(PublicScoreGuard).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments); }
// Execute the production prefix logic against game-shaped fixtures. This does not test Mono detour installation.
Check(!Prefix("BlockResult", new object[0]), "Result prefix permits native persistence");
Check(!Prefix("BlockPlayLog", new object[0]), "Playlog prefix permits native persistence");
var upload = new PacketUpsertUserAll();
var invocationArgs = new object[] { upload, true };
Check(!Prefix("BlockUploadCreate", invocationArgs) && !(bool)invocationArgs[1], "Direct upload create not suppressed");
invocationArgs = new object[] { upload, Packet.State.Process };
Check(!Prefix("BlockUploadProcess", invocationArgs) && (Packet.State)invocationArgs[1] == Packet.State.Done, "Direct upload not completed");
var generic = new Packet(); invocationArgs = new object[] { generic, new UpsertUserAll(), true };
Check(!Prefix("BlockGenericCreate", invocationArgs) && !(bool)invocationArgs[2], "Generic upload escaped filter");
generic.SetQuery(new UpsertUserAll()); invocationArgs = new object[] { generic, Packet.State.Process };
Check(!Prefix("BlockGenericProcess", invocationArgs) && (Packet.State)invocationArgs[1] == Packet.State.Done, "Generic upload proc escaped filter");
var ordinary = new Packet(); invocationArgs = new object[] { ordinary, new OtherQuery(), false };
Check(Prefix("BlockGenericCreate", invocationArgs), "Unrelated query blocked");
ordinary.SetQuery(new OtherQuery()); invocationArgs = new object[] { ordinary, Packet.State.Ready };
Check(Prefix("BlockGenericProcess", invocationArgs), "Unrelated processing blocked");
var field = typeof(Packet).GetField("state_", BindingFlags.Instance | BindingFlags.NonPublic);
Check((Packet.State)field.GetValue(upload) == Packet.State.Done && (Packet.State)field.GetValue(generic) == Packet.State.Done, "Native packet completion field not set");
Check(upload.Creates == 0 && upload.Processes == 0 && generic.Creates == 0 && generic.Processes == 0, "Fixture native request code executed");
Console.WriteLine("PASS: production prefix logic, unconditional result/playlog skips, direct/generic upload filters, completed packet state and unrelated-query passthrough. Harmony detour and in-game/server behavior are not tested.");

namespace GekiDrive
{
    internal sealed class Log { internal void LogInfo(string text) { } }
    internal static class Plugin { internal static Log SharedLog = new(); }
}
namespace MU3.Battle
{
    public sealed class SessionResult { }
    public sealed class GameEngine
    {
        public int Saved;
        [MethodImpl(MethodImplOptions.NoInlining)] public void applyResultToUserData(SessionResult result) { Saved++; }
    }
}
namespace MU3.User
{
    public sealed class UserLocal
    {
        public int Logs;
        [MethodImpl(MethodImplOptions.NoInlining)] public void addPlayLog(MU3.Client.UserPlayLog log) { Logs++; }
    }
}
namespace MU3.Client
{
    public sealed class UserPlayLog { }
    public interface INetQuery { }
    public sealed class UpsertUserAll : INetQuery { }
    public sealed class OtherQuery : INetQuery { }
    public class Packet
    {
        public enum State { Ready, Process, Done, RetryWait, Dialog, Error }
        public enum Status { OK, Error_Create = -1 }
        private State state_ = State.Ready;
        private Status status_ = Status.OK;
        public INetQuery Query { get; private set; }
        public int Creates, Processes;
        public void SetQuery(INetQuery query) { Query = query; }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool create(INetQuery query) { Creates++; Query = query; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] public virtual State proc() { Processes++; return state_ == State.Done && status_ == Status.OK ? State.Done : State.Process; }
    }
    public sealed class PacketUpsertUserAll : Packet
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public bool create(bool force, bool logout) { Creates++; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] public override State proc() { Processes++; return State.Process; }
    }
}
