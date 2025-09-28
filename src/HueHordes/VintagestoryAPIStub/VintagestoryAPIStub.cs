// VintagestoryAPI Stub - Minimal implementation for CI/CD builds only
// NOT FOR PRODUCTION USE - Does not implement actual game functionality

using System;
using System.Collections.Generic;
using System.Text.Json;
using Vintagestory.API.MathTools;

namespace Vintagestory.API.MathTools
{
    public class Vec3d
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vec3d() { }
        public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double DistanceTo(Vec3d other)
        {
            double dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double SquareDistanceTo(Vec3d other)
        {
            double dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public Vec3d Clone() => new Vec3d(X, Y, Z);
        public Vec3d Set(double x, double y, double z) { X = x; Y = y; Z = z; return this; }
        public Vec3d Set(Vec3d other) { X = other.X; Y = other.Y; Z = other.Z; return this; }
        public Vec3d Add(Vec3d other) { X += other.X; Y += other.Y; Z += other.Z; return this; }
        public Vec3d Add(double x, double y, double z) { X += x; Y += y; Z += z; return this; }
        public Vec3d Sub(Vec3d other) { X -= other.X; Y -= other.Y; Z -= other.Z; return this; }
        public Vec3d Mul(double factor) { X *= factor; Y *= factor; Z *= factor; return this; }
        public Vec3d Normalize()
        {
            double length = Math.Sqrt(X * X + Y * Y + Z * Z);
            if (length > 0) { X /= length; Y /= length; Z /= length; }
            return this;
        }

        public static Vec3d operator +(Vec3d a, Vec3d b) => new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3d operator -(Vec3d a, Vec3d b) => new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3d operator *(Vec3d a, double factor) => new Vec3d(a.X * factor, a.Y * factor, a.Z * factor);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    public class BlockPos
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public BlockPos() { }
        public BlockPos(int x, int y, int z) { X = x; Y = y; Z = z; }
        public BlockPos(Vec3d vec) { X = (int)vec.X; Y = (int)vec.Y; Z = (int)vec.Z; }

        public double DistanceTo(BlockPos other)
        {
            double dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public int ManhattenDistance(BlockPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);
        public BlockPos Add(int x, int y, int z) => new BlockPos(X + x, Y + y, Z + z);
        public BlockPos Add(BlockPos other) => new BlockPos(X + other.X, Y + other.Y, Z + other.Z);
        public Vec3d ToVec3d() => new Vec3d(X, Y, Z);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}

namespace Vintagestory.API.Common
{
    public abstract class ModSystem
    {
        public virtual void Start(ICoreAPI api) { }
        public virtual void StartServerSide(ICoreServerAPI api) { }
        public virtual void StartClientSide(ICoreClientAPI api) { }
        public virtual void AssetsLoaded(ICoreAPI api) { }
        public virtual void Dispose() { }
        public virtual bool ShouldLoad(EnumAppSide side) => true;
    }

    public interface ICoreAPI
    {
        ILogger Logger { get; }
        IWorldAccessor World { get; }
        IAssetManager Assets { get; }
        IPlayerManager Players { get; }
        string GetVersion();
        void RegisterCommand(string command, string description, CommandDelegate handler);
    }

    public interface ICoreServerAPI : ICoreAPI
    {
        new IServerPlayerManager Players { get; }
        void RegisterCommand(string command, string description, string privilege, CommandDelegate handler);
        IServerNetworkChannel Network { get; }
        IServerEventAPI Event { get; }
        void BroadcastMessageToAllGroups(string message, EnumChatType chatType);
    }

    public interface ICoreClientAPI : ICoreAPI
    {
        IClientPlayer Player { get; }
    }

    public interface IWorldAccessor
    {
        IBlockAccessor BlockAccessor { get; }
        long ElapsedMilliseconds { get; }
        IEntitySpawner EntitySpawner { get; }
        IPlayer NearestPlayer(double x, double y, double z);
        Entity[] GetEntitiesAround(Vec3d position, float radius, float height);
        IServerPlayer[] GetPlayersAround(Vec3d position, float radius, float height);
        Entity GetNearestEntity(Vec3d position, float radius, float height);
    }

    public interface IBlockAccessor
    {
        Block GetBlock(BlockPos pos);
        void SetBlock(int blockId, BlockPos pos);
        int GetBlockId(BlockPos pos);
    }

    public interface IEntitySpawner
    {
        bool SpawnEntity(Entity entity);
    }

    public interface IAssetManager
    {
        T TryGet<T>(AssetLocation location) where T : class;
        bool Exists(AssetLocation location);
    }

    public interface IPlayerManager
    {
        IPlayer[] OnlinePlayers { get; }
        IPlayer GetPlayerByName(string name);
    }

    public interface IServerPlayerManager : IPlayerManager
    {
        new IServerPlayer[] OnlinePlayers { get; }
        new IServerPlayer GetPlayerByName(string name);
    }

    public interface IPlayer
    {
        string PlayerName { get; }
        Entity Entity { get; }
        PlayerUID PlayerUID { get; }
        Vec3d WorldData { get; }
        IPlayerRole Role { get; }
    }

    public interface IServerPlayer : IPlayer
    {
        Vec3d GetSpawnPosition(bool checkWorldSpawn);
        void SendMessage(int groupId, string message, EnumChatType chatType);
        void SendMessage(string message, EnumChatType chatType);
        BlockPos GetBed();
        new Entity Entity { get; }
        IServerPlayerData PlayerData { get; }
    }

    public interface IClientPlayer : IPlayer
    {
        // Client-specific methods
    }

    public interface IPlayerRole
    {
        string Code { get; }
        bool HasPrivilege(string privilege);
    }

    public interface IServerPlayerData
    {
        byte[] Data { get; set; }
        void MarkDirty();
    }

    public interface ILogger
    {
        void Debug(string message, params object[] args);
        void Notification(string message, params object[] args);
        void Warning(string message, params object[] args);
        void Error(string message, params object[] args);
        void Fatal(string message, params object[] args);
    }

    public interface IServerNetworkChannel
    {
        void SendPacket<T>(T packet, IServerPlayer player);
        void BroadcastPacket<T>(T packet);
    }

    public interface IServerEventAPI
    {
        void PlayerJoin(IServerPlayer player);
        void PlayerLeave(IServerPlayer player);
        void OnPlayerDeath(IServerPlayer player, DamageSource damageSource);
    }

    public class Entity
    {
        public long EntityId { get; set; }
        public Vec3d Pos { get; set; } = new Vec3d();
        public EntityProperties Properties { get; set; }
        public bool Alive { get; set; } = true;
        public IWorldAccessor World { get; set; }

        public virtual void Initialize(EntityProperties properties, ICoreAPI api, long entityId) { }
        public virtual void OnGameTick(float dt) { }
        public virtual void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null) { }
        public virtual void TeleportTo(Vec3d position) { }
        public virtual bool ShouldDespawn() => !Alive;
    }

    public class EntityProperties
    {
        public AssetLocation Code { get; set; }
        public string Habitat { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }

    public class Block
    {
        public int Id { get; set; }
        public AssetLocation Code { get; set; }
        public string BlockMaterial { get; set; }
        public bool IsReplacableBy(Block block) => false;
    }

    public class AssetLocation
    {
        public string Domain { get; set; }
        public string Path { get; set; }

        public AssetLocation(string path)
        {
            var parts = path.Split(':');
            Domain = parts.Length > 1 ? parts[0] : "game";
            Path = parts.Length > 1 ? parts[1] : parts[0];
        }

        public AssetLocation(string domain, string path) { Domain = domain; Path = path; }

        public override string ToString() => $"{Domain}:{Path}";
    }

    public class PlayerUID
    {
        public string Id { get; set; }
        public PlayerUID(string id) { Id = id; }
        public override string ToString() => Id;
    }

    public class DamageSource
    {
        public EnumDamageSource Source { get; set; }
        public EnumDamageType Type { get; set; }
        public Entity SourceEntity { get; set; }
    }

    public delegate void CommandDelegate(IServerPlayer player, int groupId, CmdArgs args);

    public class CmdArgs
    {
        public string[] Args { get; set; } = new string[0];
        public string this[int index] => index < Args.Length ? Args[index] : null;
        public int Length => Args.Length;
    }

    public enum EnumAppSide { Universal, Server, Client }
    public enum EnumChatType { OthersMessage, OwnMessage, Notification, CommandSuccess, CommandError }
    public enum EnumDespawnReason { Death, Unload, Expire, Removed }
    public enum EnumDamageSource { Player, Block, Entity, Environment, Internal, Unknown }
    public enum EnumDamageType { PiercingAttack, SlashingAttack, BluntAttack, Gravity, Fire, Suffocation }
}

namespace Vintagestory.API.Server
{
    public class Role
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] Privileges { get; set; } = new string[0];

        public bool HasPrivilege(string privilege) => Array.IndexOf(Privileges, privilege) >= 0;
    }
}

namespace Vintagestory.API.Datastructures
{
    public class TreeAttribute : ITreeAttribute
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public int GetInt(string key, int defaultValue = 0) => _data.TryGetValue(key, out var value) && value is int ? (int)value : defaultValue;
        public void SetInt(string key, int value) => _data[key] = value;
        public bool GetBool(string key, bool defaultValue = false) => _data.TryGetValue(key, out var value) && value is bool ? (bool)value : defaultValue;
        public void SetBool(string key, bool value) => _data[key] = value;
        public string GetString(string key, string defaultValue = null) => _data.TryGetValue(key, out var value) && value is string ? (string)value : defaultValue;
        public void SetString(string key, string value) => _data[key] = value;
        public byte[] ToBytes() => System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_data));
        public void FromBytes(byte[] data)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(System.Text.Encoding.UTF8.GetString(data));
            _data.Clear();
            foreach (var kvp in dict) _data[kvp.Key] = kvp.Value;
        }
        public ITreeAttribute Clone()
        {
            var clone = new TreeAttribute();
            foreach (var kvp in _data) clone._data[kvp.Key] = kvp.Value;
            return clone;
        }
    }

    public interface ITreeAttribute
    {
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);
        string GetString(string key, string defaultValue = null);
        void SetString(string key, string value);
        byte[] ToBytes();
        void FromBytes(byte[] data);
        ITreeAttribute Clone();
    }
}

namespace Vintagestory.API.Config
{
    public static class GlobalConstants
    {
        public const int DefaultServerPort = 42420;
        public const string CurrentGameVersion = "1.19.0";
        public const int MaxClients = 32;
    }

    public static class GameMath
    {
        public const double PI = Math.PI;
        public const double DEG2RAD = PI / 180.0;
        public const double RAD2DEG = 180.0 / PI;

        public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}

namespace Vintagestory.API.Util
{
    public static class ColorUtil
    {
        public static int ColorFromRgba(int r, int g, int b, int a) => (a << 24) | (r << 16) | (g << 8) | b;
        public static void ColorToRgba(int color, out int r, out int g, out int b, out int a)
        {
            a = (color >> 24) & 255;
            r = (color >> 16) & 255;
            g = (color >> 8) & 255;
            b = color & 255;
        }
    }

    public static class SerializerUtil
    {
        public static byte[] Serialize<T>(T obj) => System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
        public static T Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(System.Text.Encoding.UTF8.GetString(data));
    }
}