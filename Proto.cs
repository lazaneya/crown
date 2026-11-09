#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Godot;
using ProtoBuf;

namespace CrownRelayServer
{
    [ProtoContract]
    public partial class PCommand : RefCounted
    {
        [ProtoMember(1)]
        [DefaultValue(PDestination.All)]
        public PDestination Destination = PDestination.All;

        [ProtoMember(2)]
        [DefaultValue("0")]
        public string ObjectID = "0";

        [ProtoMember(11)] public string? ObjectTypeString;
        [ProtoMember(12)] public string? MethodStringName;
        [ProtoMember(13)] public bool? IsHost;
        [ProtoMember(14)] public string? PlayerName;
        [ProtoMember(15)] public int? LobbyId;

        [ProtoMember(21)] public Dictionary<string, string> Kwargs = new();
        [ProtoMember(22)] public string[] Args = Array.Empty<string>();

        public byte[] Serialize()
        {
            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.Serialize(ms, this);
                buffer = ms.ToArray();
            }

            return buffer;
        }
    }

    public enum PDestination
    {
        RelayServer = 0,
        All = 1,
        Host = 2,
        Clients = 3
    }
}
