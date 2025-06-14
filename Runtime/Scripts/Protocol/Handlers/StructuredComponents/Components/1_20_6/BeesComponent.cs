#nullable enable
using System;
using System.Collections.Generic;
using CraftSharp.Protocol.Handlers.StructuredComponents.Core;

namespace CraftSharp.Protocol.Handlers.StructuredComponents.Components
{
    public record BeesComponent : StructuredComponent
    {
        public int NumberOfBees { get; set; }
        public List<Bee> Bees { get; set; } = new();

        public BeesComponent(ItemPalette itemPalette, SubComponentRegistry subComponentRegistry) 
            : base(itemPalette, subComponentRegistry)
        {

        }
        
        public override void Parse(IMinecraftDataTypes dataTypes, Queue<byte> data)
        {
            NumberOfBees = DataTypes.ReadNextVarInt(data);
            for (var i = 0; i < NumberOfBees; i++)
            {
                Bees.Add(new Bee(DataTypes.ReadNextNbt(data, dataTypes.UseAnonymousNBT), DataTypes.ReadNextVarInt(data), DataTypes.ReadNextVarInt(data)));
            }
        }

        public override Queue<byte> Serialize(IMinecraftDataTypes dataTypes)
        {
            var data = new List<byte>();
            data.AddRange(DataTypes.GetVarInt(NumberOfBees));

            if (NumberOfBees > 0)
            {
                if (NumberOfBees != Bees.Count)
                    throw new Exception("Can't serialize the BeeComponent because NumberOfBees and Bees.Count differ!");
                
                foreach (var bee in Bees)
                {
                    data.AddRange(DataTypes.GetNbt(bee.EntityDataNbt));
                    data.AddRange(DataTypes.GetVarInt(bee.TicksInHive));
                    data.AddRange(DataTypes.GetVarInt(bee.MinTicksInHive));
                }
            }

            return new Queue<byte>(data);
        }
    }

    public record Bee(Dictionary<string, object>? EntityDataNbt, int TicksInHive, int MinTicksInHive)
    {
        public Dictionary<string, object>? EntityDataNbt { get; } = EntityDataNbt;
        public int TicksInHive { get; } = TicksInHive;
        public int MinTicksInHive { get; } = MinTicksInHive;
    }
}