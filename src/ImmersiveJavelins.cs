using System;
using HarmonyLib;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using System.Collections.Generic;

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
		public static ICoreClientAPI capi;
		public static ICoreServerAPI sapi;
		private MeshRef _circleMesh;
		private readonly Dictionary<string, float> craftingStartTimes = new Dictionary<string, float>();

		private bool isAnimating = false;

		private readonly int boneJavelinCraftTime = 1500;
		private readonly int fletchingCraftingTime = 1000;

		private static bool alreadySentMessageThisAction = false;


		public override void StartServerSide(ICoreServerAPI api)
		{
			sapi = api;
			sapi.Event.RegisterGameTickListener(new Action<float>(this.OnGameTick), 50, 0);
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			capi = api;
			api.Event.RegisterGameTickListener(new Action<float>(this.OnClientTick), 50, 0);
		}


		// Crafting immersivejavelins:javelinhead-bone Section

		private void OnGameTick(float dt)
		{
			bool flag = sapi == null;
			if (sapi != null && sapi.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
			{
				IPlayer[] allPlayers = sapi.World.AllOnlinePlayers;
				
				for (int i = 0; i < allPlayers.Length; i++)
				{
					IPlayer player = allPlayers[i];
					IServerPlayer serverPlayer = player as IServerPlayer;

					if (serverPlayer != null && serverPlayer.ConnectionState == EnumClientState.Playing)
					{
						string playerUID = (serverPlayer != null) ? serverPlayer.PlayerUID : null;

						if (playerUID != null)
						{
							bool rightMouseDown = serverPlayer.Entity.Controls.RightMouseDown;
							if (rightMouseDown) {
								ItemSlot itemSlot = null;
								ItemSlot leftHandItemSlot = null;
								if (serverPlayer != null)
								{
									IPlayerInventoryManager inventoryManager = serverPlayer.InventoryManager;
									itemSlot = ((inventoryManager != null) ? inventoryManager.ActiveHotbarSlot : null);
									leftHandItemSlot = serverPlayer.Entity != null && serverPlayer.Entity.LeftHandItemSlot != null ? serverPlayer.Entity.LeftHandItemSlot : null;
								}

								string itemClass;
								string itemCodePath;
								if (itemSlot == null)
								{
									itemClass = null;
									itemCodePath = null;
								}
								else
								{
									ItemStack itemstack = itemSlot.Itemstack;
									if (itemstack == null)
									{
										itemClass = null;
										itemCodePath = null;
									}
									else
									{
										CollectibleObject collectible = itemstack.Collectible;
										if (collectible == null)
										{
											itemClass = null;
											itemCodePath = null;
										}
										else
										{
											itemClass = collectible?.Class;
											itemCodePath = itemstack?.Item?.Code?.Path;
										}
									}
								}

								if(itemClass == "ItemKnife") {
									if(leftHandItemSlot?.Itemstack?.Collectible.Code.Path == "bone") {
										bool playerCrafting = this.craftingStartTimes.ContainsKey(playerUID);
										if (!playerCrafting) {
											// Get him crafting
											capi.World.Player.Entity.Attributes.SetBool("isCrafting", true);
											// Define an array of possible sounds
											string[] soundEffects = { "game:sounds/player/chalkdraw1", "game:sounds/player/chalkdraw2", "game:sounds/player/chalkdraw3" };

											// Select a random sound effect
											int randomIndex = new Random().Next(soundEffects.Length);
											string selectedSound = soundEffects[randomIndex];

											// Play the selected sound effect
											sapi.World.PlaySoundAt(new AssetLocation(selectedSound), serverPlayer.Entity.Pos.X, serverPlayer.Entity.Pos.Y, serverPlayer.Entity.Pos.Z, null, true, 32f, 1f);
											this.craftingStartTimes[playerUID] = (float)sapi.World.ElapsedMilliseconds;
										} else {
											float heldDuration = (float)sapi.World.ElapsedMilliseconds - this.craftingStartTimes[playerUID];
											if (heldDuration >= (float)this.boneJavelinCraftTime)
											{
												this.CraftJavelinHeads(serverPlayer, leftHandItemSlot, serverPlayer);
												itemSlot?.Itemstack?.Collectible.DamageItem(sapi.World, serverPlayer.Entity, itemSlot, 1);
												this.craftingStartTimes.Remove(playerUID);
												capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
											}
										}
									} else if(leftHandItemSlot?.Itemstack?.Collectible.Code.Path == "feather") {
										bool playerCrafting = this.craftingStartTimes.ContainsKey(playerUID);
										if (!playerCrafting) {
											// Get him crafting
											capi.World.Player.Entity.Attributes.SetBool("isCrafting", true);
											// Define an array of possible sounds
											string[] soundEffects = { "game:sounds/player/gluerepair1", "game:sounds/player/gluerepair2", "game:sounds/player/gluerepair3", "game:sounds/player/gluerepair4" };

											// Select a random sound effect
											int randomIndex = new Random().Next(soundEffects.Length);
											string selectedSound = soundEffects[randomIndex];

											// Play the selected sound effect
											sapi.World.PlaySoundAt(new AssetLocation(selectedSound), serverPlayer.Entity.Pos.X, serverPlayer.Entity.Pos.Y, serverPlayer.Entity.Pos.Z, null, true, 32f, 1f);
											this.craftingStartTimes[playerUID] = (float)sapi.World.ElapsedMilliseconds;
										} else {
											float heldDuration = (float)sapi.World.ElapsedMilliseconds - this.craftingStartTimes[playerUID];
											if (heldDuration >= (float)this.fletchingCraftingTime)
											{
												this.CraftJavelinFletchings(serverPlayer, leftHandItemSlot, serverPlayer);
												itemSlot?.Itemstack?.Collectible.DamageItem(sapi.World, serverPlayer.Entity, itemSlot, 1);
												this.craftingStartTimes.Remove(playerUID);
												capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
											}
										}
									} else {
										this.craftingStartTimes.Remove(playerUID);
										capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
									}
								} else {
									if(itemCodePath == "javelinhead-bone") {
										bool playerCrafting = this.craftingStartTimes.ContainsKey(playerUID);
										ItemSlot stickSlot = this.FindItemInHotBarOrBackpack(serverPlayer, "stick");
										if (!playerCrafting) {
											// Get him crafting
											if(stickSlot == null) {
												if(stickSlot == null && !alreadySentMessageThisAction) serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("I need a stick to attach that to. I could also grab some fletching while I am at it..."), EnumChatType.Notification);
												alreadySentMessageThisAction = true;
												return;
											}
											capi.World.Player.Entity.Attributes.SetBool("isCrafting", true);
											sapi.World.PlaySoundAt(new AssetLocation("game:sounds/bow-draw"), serverPlayer.Entity.Pos.X, serverPlayer.Entity.Pos.Y, serverPlayer.Entity.Pos.Z, null, true, 32f, 1f);
											this.craftingStartTimes[playerUID] = (float)sapi.World.ElapsedMilliseconds;
										} else {
											float heldDuration = (float)sapi.World.ElapsedMilliseconds - this.craftingStartTimes[playerUID];
											if (heldDuration >= (float)this.boneJavelinCraftTime)
											{
												this.CraftJavelin(serverPlayer, itemSlot, stickSlot, null, null);
												this.craftingStartTimes.Remove(playerUID);
												capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
											}
										}
									} else if(itemCodePath == "javelinfletching") {
										bool playerCrafting = this.craftingStartTimes.ContainsKey(playerUID);
										ItemSlot stickSlot = this.FindItemInHotBarOrBackpack(serverPlayer, "stick");
										ItemSlot javelinheadSlot = this.FindItemInHotBarOrBackpack(serverPlayer, "javelinhead-bone");
										ItemSlot crudeJavelinSlot = this.FindItemInHotBarOrBackpack(serverPlayer, "crudejavelin-bone");
										if (!playerCrafting) {
											// Get him crafting
											if((stickSlot == null || javelinheadSlot == null) && crudeJavelinSlot == null) {
												if(stickSlot == null && crudeJavelinSlot == null && !alreadySentMessageThisAction) serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("I need a stick or a crude javelin to attach that to."), EnumChatType.Notification);
												if(javelinheadSlot == null && !alreadySentMessageThisAction) serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("I am missing a javelin head to assemble this."), EnumChatType.Notification);
												alreadySentMessageThisAction = true;
												return;
											}
											capi.World.Player.Entity.Attributes.SetBool("isCrafting", true);
											sapi.World.PlaySoundAt(new AssetLocation("game:sounds/bow-draw"), serverPlayer.Entity.Pos.X, serverPlayer.Entity.Pos.Y, serverPlayer.Entity.Pos.Z, null, true, 32f, 1f);
											this.craftingStartTimes[playerUID] = (float)sapi.World.ElapsedMilliseconds;
										} else {
											float heldDuration = (float)sapi.World.ElapsedMilliseconds - this.craftingStartTimes[playerUID];
											if (heldDuration >= (float)this.boneJavelinCraftTime)
											{
												this.CraftJavelin(serverPlayer, javelinheadSlot, stickSlot, itemSlot, crudeJavelinSlot);
												this.craftingStartTimes.Remove(playerUID);
												capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
											}
										}
									} else {
										this.craftingStartTimes.Remove(playerUID);
										capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
									}
								}
							} else {
								alreadySentMessageThisAction = false;
								this.craftingStartTimes.Remove(playerUID);
								capi.World.Player.Entity.Attributes.SetBool("isCrafting", false);
							}
						}
					}
				}
			}
		}

		private void OnClientTick(float dt)
		{
			if (capi == null) return;

			var world = capi.World;
			var clientPlayer = world?.Player;
			var serverPlayer = clientPlayer as IServerPlayer;
			var entityPlayer = clientPlayer?.Entity;

			// Check if item path contains "head" or "knifeblade"
			bool playerCrafting = entityPlayer.Attributes.GetBool("isCrafting");

			if (playerCrafting)
			{
				if (!this.isAnimating && entityPlayer != null)
				{
					this.StartCraftAnimation(entityPlayer);
					this.isAnimating = true;
				}
			}
			else if (this.isAnimating && entityPlayer != null)
			{
				this.StopCraftAnimation(entityPlayer);
				this.isAnimating = false;
			}
		}

		private void StartCraftAnimation(Entity entity)
		{
			AnimationMetaData animationMetaData = new AnimationMetaData
			{
				Animation = "squeezehoneycomb",
				Code = "squeezehoneycomb",
				EaseInSpeed = 7f,
				EaseOutSpeed = 7f,
				Weight = 8f,
				BlendMode = EnumAnimationBlendMode.AddAverage,
				ElementWeight = new Dictionary<string, float>
				{
					{
						"UpperArmR",
						200f
					},
					{
						"LowerArmR",
						200f
					},
					{
						"UpperArmL",
						200f
					},
					{
						"LowerArmL",
						200f
					},
					{
						"ItemAnchor",
						40f
					}
				},
				ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode>
				{
					{
						"UpperArmR",
						EnumAnimationBlendMode.AddAverage
					},
					{
						"LowerArmR",
						EnumAnimationBlendMode.AddAverage
					},
					{
						"UpperArmL",
						EnumAnimationBlendMode.AddAverage
					},
					{
						"LowerArmL",
						EnumAnimationBlendMode.AddAverage
					},
					{
						"ItemAnchor",
						EnumAnimationBlendMode.AddAverage
					}
				}
			};
			entity.AnimManager.StartAnimation(animationMetaData.Init());
		}

		private void StopCraftAnimation(Entity entity)
		{
			entity.AnimManager.StopAnimation("squeezehoneycomb");
		}

		private ItemSlot FindItemInHotBarOrBackpack(IServerPlayer player, string wantedPath)
		{
			IInventory inventory;
			IInventory inventory2;
			if (player == null)
			{
				return null;
			}

			IPlayerInventoryManager inventoryManager = player.InventoryManager;
			inventory = ((inventoryManager != null) ? inventoryManager.GetHotbarInventory() : null);
			inventory2 = ((inventoryManager != null) ? inventoryManager.GetOwnInventory("backpack") : null);
			if (inventory == null)
			{
				return null;
			}

			for (int i = 0; i < inventory.Count; i++)
			{
				ItemSlot itemSlot = inventory[i];

				if (itemSlot == null)
				{
					continue;
				}
				ItemStack itemstack = itemSlot.Itemstack;
				if (itemstack == null)
				{
					continue;
				}

				CollectibleObject collectible = itemstack.Collectible;
				if (collectible == null)
				{
					continue;
				}

				AssetLocation code = collectible.Code;
				if (code == null)
				{
					continue;
				}

				string path = code.Path;
				if(path != null && path == wantedPath) {
					return itemSlot;
				}
			}
			if (inventory == null)
			{
				return null;
			}

			for (int i = 0; i < inventory2.Count; i++)
			{
				ItemSlot itemSlot = inventory2[i];

				if (itemSlot == null)
				{
					continue;
				}
				ItemStack itemstack = itemSlot.Itemstack;
				if (itemstack == null)
				{
					continue;
				}

				CollectibleObject collectible = itemstack.Collectible;
				if (collectible == null)
				{
					continue;
				}

				AssetLocation code = collectible.Code;
				if (code == null)
				{
					continue;
				}

				string path = code.Path;
				if(path != null && path == wantedPath) {
					return itemSlot;
				}
			}
			return null;
		}
		private void CraftJavelinHeads(IServerPlayer player, ItemSlot leftHandItemSlot, IServerPlayer serverPlayer) {
			if(leftHandItemSlot != null && leftHandItemSlot.Itemstack != null && sapi != null) {
				leftHandItemSlot.TakeOut(1);

				Item javelinHead = sapi.World.GetItem(new AssetLocation("immersivejavelins:javelinhead-bone"));
				ItemStack javelinHeadStack = new ItemStack(javelinHead, 2);
				bool itemGiven = player.InventoryManager.TryGiveItemstack(javelinHeadStack, false);
				if(itemGiven) {
					leftHandItemSlot.MarkDirty();
				} else {
					serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("My invetory is full... I can't craft that."), EnumChatType.Notification);
				}
			}
		}

		private void CraftJavelinFletchings(IServerPlayer player, ItemSlot leftHandItemSlot, IServerPlayer serverPlayer) {
			if(leftHandItemSlot != null && leftHandItemSlot.Itemstack != null && sapi != null) {
				leftHandItemSlot.TakeOut(1);

				Item javelingFletchings = sapi.World.GetItem(new AssetLocation("immersivejavelins:javelinfletching"));
				ItemStack javelingFletchingsStack = new ItemStack(javelingFletchings, 1);
				bool itemGiven = player.InventoryManager.TryGiveItemstack(javelingFletchingsStack, false);
				if(itemGiven) {
					leftHandItemSlot.MarkDirty();
				} else {
					serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("My invetory is full... I can't craft that."), EnumChatType.Notification);

				}
			}
		}

		private void CraftJavelin(IServerPlayer player, ItemSlot javelinHeadSlot, ItemSlot stickSlot, ItemSlot fletchingSlot, ItemSlot crudeJavelinSlot)
		{
			// Check if the player has the necessary items in the slots
			bool hasJavelinHead = javelinHeadSlot?.Itemstack != null;
			bool hasStick = stickSlot?.Itemstack != null;
			bool hasFletching = fletchingSlot?.Itemstack != null;
			bool hasCrudeJavelin = crudeJavelinSlot?.Itemstack != null;

			// Determine the type of javelin to create
			if (hasFletching && (hasCrudeJavelin || (hasStick && hasJavelinHead)))
			{
				// Create bone javelin
				Item boneJavelin = sapi.World.GetItem(new AssetLocation("immersivejavelins:javelin-bone"));
				ItemStack boneJavelinStack = new ItemStack(boneJavelin, 1);

				// Attempt to give the bone javelin to the player
				if (player.InventoryManager.TryGiveItemstack(boneJavelinStack, false))
				{
					if (hasCrudeJavelin)
					{
						crudeJavelinSlot.TakeOut(1);
						crudeJavelinSlot.MarkDirty();
					}
					else
					{
						javelinHeadSlot.TakeOut(1);
						stickSlot.TakeOut(1);
						javelinHeadSlot.MarkDirty();
						stickSlot.MarkDirty();
					}
					fletchingSlot.TakeOut(1);
					fletchingSlot.MarkDirty();
				}
				else
				{
					player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("My invetory is full... I can't craft that."), EnumChatType.Notification);
				}
			}
			else if (hasJavelinHead && hasStick && !hasFletching)
			{
				// Create crude javelin
				Item crudeJavelin = sapi.World.GetItem(new AssetLocation("immersivejavelins:crudejavelin-bone"));
				ItemStack crudeJavelinStack = new ItemStack(crudeJavelin, 1);

				// Attempt to give the crude javelin to the player
				if (player.InventoryManager.TryGiveItemstack(crudeJavelinStack, false))
				{
					javelinHeadSlot.TakeOut(1);
					stickSlot.TakeOut(1);
					javelinHeadSlot.MarkDirty();
					stickSlot.MarkDirty();
				}
				else
				{
					player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("My invetory is full... I can't craft that."), EnumChatType.Notification);
				}
			}
			else
			{
				// sapi.World.Logger.Event("Player lacks required items to craft javelin.");
			}
		}

		public override void Start(ICoreAPI api)
		{

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
			ImmersiveJavelinsMod.capi?.World.AddCameraShake(0.17f);

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
			Vec3d spawnPos = byEntity.ServerPos.AheadCopy(0.21).XYZ.Add(byEntity.LocalEyePos.X, byEntity.LocalEyePos.Y - 0.2, byEntity.LocalEyePos.Z);
			enpr.ServerPos.SetPosWithDimension(spawnPos);
			enpr.ServerPos.Motion.Set(velocity);

			enpr.Pos.SetFrom(enpr.ServerPos);
			enpr.World = byEntity.World;
			enpr.SetRotation();

			byEntity.World.SpawnEntity(enpr);
			byEntity.StartAnimation("throw");

			if (byEntity is EntityPlayer) co.RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible is ItemSpear);

            var pitch = (byEntity as EntityPlayer).talkUtil.pitchModifier;
            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)ImmersiveJavelinsMod.capi.World.Rand.NextDouble() * 0.2f, 16, 0.35f);

			return false;
		}
	}

}
