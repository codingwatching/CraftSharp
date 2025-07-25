using System.Collections.Generic;
using CraftSharp.Protocol.Handlers.StructuredComponents.Core;

namespace CraftSharp.Protocol.Handlers.StructuredComponents.Components
{
    public record BlockStateComponent : StructuredComponent
    {
        public int NumberOfProperties { get; set; }
        public List<(string, string)> Properties { get; set; } = new();
        
        public BlockStateComponent(ItemPalette itemPalette, SubComponentRegistry subComponentRegistry) 
            : base(itemPalette, subComponentRegistry)
        {

        }

        public override void Parse(IMinecraftDataTypes dataTypes, Queue<byte> data)
        {
            NumberOfProperties = DataTypes.ReadNextVarInt(data);
            for(var i = 0; i < NumberOfProperties; i++)
                Properties.Add((DataTypes.ReadNextString(data), DataTypes.ReadNextString(data)));
        }

        public override Queue<byte> Serialize(IMinecraftDataTypes dataTypes)
        {
            var data = new List<byte>();
            data.AddRange(DataTypes.GetVarInt(NumberOfProperties));
            for (var i = 0; i < NumberOfProperties; i++)
            {
                data.AddRange(DataTypes.GetString(Properties[i].Item1));
                data.AddRange(DataTypes.GetString(Properties[i].Item2));
            }
                
            return new Queue<byte>(data);
        }
    }
}