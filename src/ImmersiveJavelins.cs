using HarmonyLib;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;

using Vintagestory.GameContent;

[assembly: ModInfo(
    "Immersive Javelins",                                  // Name
    "immersivejavelins"                                  // Mod ID
)]

namespace ImmersiveJavelins
{
	public class ImmersiveJavelinsMod : ModSystem
	{
		// Store the ICoreAPI reference
		public static ICoreAPI api;
		public override void Start(ICoreAPI api)
		{
			ImmersiveJavelinsMod.api = api;
			var harmony = new Harmony("immersivejavelins");

			var original = typeof(ItemSpear).GetMethod("OnHeldInteractStop");
			var prefix = typeof(EntityPlayer_LightHsv_Patched).GetMethod("OnHeldInteractStop_Prefix");

			harmony.Patch(original, prefix: new HarmonyMethod(prefix));

			var original2 = typeof(CollectibleObject).GetMethod("OnHeldAttackStart");
			var prefix2 = typeof(EntityPlayer_LightHsv_Patched).GetMethod("OnHeldAttackStart_Prefix");

			harmony.Patch(original2, prefix: new HarmonyMethod(prefix2));

			var original3 = typeof(ItemSpear).GetMethod("GetHeldItemInfo");
			var prefix3 = typeof(EntityPlayer_LightHsv_Patched).GetMethod("GetHeldItemInfo_Prefix");

			harmony.Patch(original3, prefix: new HarmonyMethod(prefix3));

		}
		
		// public override void StartClientSide(ICoreClientAPI api)
		// {
			
		// }
		
		// public override void StartServerSide(ICoreServerAPI api)
		// {
		
		// }
	}

    public class EntityPlayer_LightHsv_Patched
    {

		public static bool GetHeldItemInfo_Prefix(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
			if (inSlot.Itemstack.Collectible.Code.Domain != "immersivejavelins") return true;

            if (inSlot.Itemstack.Collectible.Attributes == null) return false;

            float damage = 1.5f;

            if (inSlot.Itemstack.Collectible.Attributes != null)
            {
                damage = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            dsc.AppendLine(damage + Lang.Get("piercing-damage-thrown"));
            float breakChanceOnImpact = inSlot.Itemstack.Collectible.Attributes["breakChanceOnImpact"].AsFloat(0.5f);
            dsc.AppendLine(Lang.Get("breakchanceonimpact", (int)(breakChanceOnImpact * 100)));

			dsc.AppendLine("\n" + inSlot.Itemstack.Collectible.GetItemDescText());
			return false;
        }

		public static bool OnHeldAttackStart_Prefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
		{
			if (slot.Itemstack.Collectible.Code.Domain != "immersivejavelins") return true;
			handling = EnumHandHandling.PreventDefault;
			return false;
		}

        public static bool OnHeldInteractStop_Prefix(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) 
		{
			if (slot.Itemstack.Collectible.Code.Domain != "immersivejavelins") return true;
			if (byEntity.Attributes.GetInt("aimingCancel") == 1) return false;

			CollectibleObject co = slot.Itemstack.Collectible;

			byEntity.Attributes.SetInt("aiming", 0);
			byEntity.StopAnimation("aim");

			if (secondsUsed < 0.35f) return false;

			float damage = slot.Itemstack.Collectible.Attributes?["damage"].AsFloat(1.5f) ?? 1.5f;
			(ImmersiveJavelinsMod.api as ICoreClientAPI)?.World.AddCameraShake(0.17f);

			// Take out one item from the stack only once here

			IPlayer byPlayer = null;
			if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

			byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

			EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(slot.Itemstack?.Collectible?.Attributes["spearEntityCode"].AsString()));
			EntityProjectile enpr = byEntity.World.ClassRegistry.CreateEntity(type) as EntityProjectile;
			ItemStack stack = slot.TakeOut(1);
			slot.MarkDirty();
			enpr.FiredBy = byEntity;
			enpr.Damage = damage;
			enpr.ProjectileStack = stack;

			// Set break chance directly on projectile for impact handling
			enpr.DropOnImpactChance = 1 - slot.Itemstack?.Collectible?.Attributes?["breakChanceOnImpact"].AsFloat() ?? 0.2f;
			enpr.DamageStackOnImpact = false;

			// Motion and velocity setup
			float acc = 1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0);
			double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
			double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;
			Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
			Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
			Vec3d velocity = (aheadPos - pos) * 0.8;
			Vec3d spawnPos = byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(byEntity.LocalEyePos.X, byEntity.LocalEyePos.Y - 0.2, byEntity.LocalEyePos.Z);
			enpr.ServerPos.SetPosWithDimension(spawnPos);
			enpr.ServerPos.Motion.Set(velocity);

			enpr.Pos.SetFrom(enpr.ServerPos);
			enpr.World = byEntity.World;
			enpr.SetRotation();

			byEntity.World.SpawnEntity(enpr);
			byEntity.StartAnimation("throw");

			if (byEntity is EntityPlayer) co.RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible is ItemSpear);

            var pitch = (byEntity as EntityPlayer).talkUtil.pitchModifier;
            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)ImmersiveJavelinsMod.api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);

			return false;
		}
	}

}
