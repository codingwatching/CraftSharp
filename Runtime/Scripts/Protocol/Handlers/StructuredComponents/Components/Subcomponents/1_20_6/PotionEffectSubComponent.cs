using System.Collections.Generic;
using CraftSharp.Protocol.Handlers.StructuredComponents.Core;

namespace CraftSharp.Protocol.Handlers.StructuredComponents.Components.Subcomponents
{
    public record PotionEffectSubComponent : SubComponent
    {
        public ResourceLocation EffectId { get; set; }
        public DetailsSubComponent Details { get; set; }

        public PotionEffectSubComponent(SubComponentRegistry subComponentRegistry)
            : base(subComponentRegistry)
        {
            
        }
        
        public override void Parse(IMinecraftDataTypes dataTypes, Queue<byte> data)
        {
            var effectNumId = DataTypes.ReadNextVarInt(data);
            EffectId = MobEffectPalette.INSTANCE.GetIdByNumId(effectNumId);
            Details = (DetailsSubComponent) SubComponentRegistry.ParseSubComponent(SubComponents.Details, data);
        }

        public override Queue<byte> Serialize(IMinecraftDataTypes dataTypes)
        {
            var data = new List<byte>();
            var effectNumId = MobEffectPalette.INSTANCE.GetNumIdById(EffectId);
            data.AddRange(DataTypes.GetVarInt(effectNumId));
            data.AddRange(Details.Serialize(dataTypes));
            return new Queue<byte>(data);
        }
    }
}