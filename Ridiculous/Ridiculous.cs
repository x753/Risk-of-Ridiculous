using BepInEx;
using RoR2;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using R2API.Utils;

namespace Ridiculousness
{
    [BepInPlugin("com.753.RiskOfRidiculous", "Risk of Ridiculous", "0.9.8")]
    [BepInDependency("com.bepis.r2api")]

    public class Ridiculous : BaseUnityPlugin
    {
        // Static variables to be used wherever necessary
        public static AssetBundle ridiculousAssetBundle;
        public static GameObject ridiculousMap;

        public static string[] ridiculousMonsters;
        public static int[] spawnDelay;

        public static float combatTimer = 0f;
		public static int combatMonsterRating = 0;
		public static int combatMonstersSpawned = 0;

        // Monster spawner locations
        public static Transform[] basicSpawner;
		public static Transform[] flyerSpawner;
		public static Transform[] crabSpawner;
		public static Transform[] vagrantSpawner;
		public static Transform[] bigSpawner;

        public void Awake()
        {
            // Allow for mod-specific matchmaking
            RoR2.RoR2Application.isModded = false;
            On.RoR2.RoR2Application.Awake += (orig, self) =>
            {
                orig(self);
                typeof(RoR2Application).GetField("steamBuildId", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, "Ridiculous 0.9.7");
            };
            On.RoR2.DisableIfGameModded.OnEnable += (orig, self) =>
            {
                RoR2.RoR2Application.isModded = false;
            };

            // Load in our map asset
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            using (Stream stream = execAssembly.GetManifestResourceStream("Ridiculous.ridiculous.assets"))
            {
                AssetBundle ridiculousAssetBundle = AssetBundle.LoadFromStream(stream);
                ridiculousMap = ridiculousAssetBundle.LoadAsset<GameObject>("Assets/ridiculous.prefab");
            }

            // Create an array of monsters we're able to spawn in our custom map
            ridiculousMonsters = new string[]
			{
				"Beetle",
				"Jellyfish",
				"Wisp",
				"Lemurian",
                "HermitCrab",
                "BeetleGuard",
                "Golem",
                "Bell",
				"GreaterWisp",
				"Vagrant",
                "LemurianBruiser",
				"Titan"
			};

            // The only stage we should load is testscene
            On.RoR2.Run.PickNextStageScene += (orig, self, choices) =>
            {
                SceneDef nextScene = ScriptableObject.CreateInstance<SceneDef>();
                nextScene.SetPropertyValue("sceneName", "testscene");
                self.nextStageScene = nextScene;
            };

            // Initialize the map on start of match
            On.RoR2.Stage.Start += (orig, self) =>
            {
                orig(self);

                UnityEngine.Object.Instantiate<GameObject>(ridiculousMap, Vector3.zero, Quaternion.identity);

			    // Destroy pre-existing garbage in the scene
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Directional Light (SUN)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretMaster"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretMaster (1)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretMaster (2)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretMaster (3)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretBody(Clone)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretBody(Clone)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretBody(Clone)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("EngiTurretBody(Clone)"));
                UnityEngine.Object.DestroyImmediate(GameObject.Find("GolemBodyInvincible(Clone)"));
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Reflection Probe"));
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Plane"));
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Plane (1)"));
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Plane (2)"));
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Plane (3)"));
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Teleporter1(Clone)"));

                // Add necessary monobehaviours
                if (NetworkServer.active) // server only
                {
                    GameObject.Find("pokeball").AddComponent<Pokeball>();
                }
                else // networking is too hard to code cleanly, just make it server only
                {
                    UnityEngine.Object.DestroyImmediate(GameObject.Find("pokeball"));
                }
			    GameObject.Find("teddy easter egg 1").AddComponent<TeddyDrop>();
			    GameObject.Find("teddy easter egg 2").AddComponent<TeddyDrop>();
			    GameObject.Find("teddy easter egg 3").AddComponent<TeddyDrop>();
			    GameObject.Find("key for keyhole").AddComponent<KeyDrop>();
                GameObject.Find("shrine of chance").AddComponent<ChanceShrine>();
                GameObject.Find("shrine of order").AddComponent<OrderShrine>();
                GameObject.Find("lakitu hook").AddComponent<ChestLakitu>();
                GameObject.Find("just a chest").AddComponent<ChestDrop>();

                // Locate monster spawn positions
                basicSpawner = GameObject.Find("basic spawner").GetComponentsInChildren<Transform>();
				flyerSpawner = GameObject.Find("flyer spawner").GetComponentsInChildren<Transform>();
				crabSpawner = GameObject.Find("crab spawner").GetComponentsInChildren<Transform>();
				vagrantSpawner = GameObject.Find("vagrant spawner").GetComponentsInChildren<Transform>();
				bigSpawner = GameObject.Find("big spawner").GetComponentsInChildren<Transform>();
				combatMonsterRating = 6; // start off only able to spawn the first 6 monsters
            };

            // Override enemy spawning behavior
            On.RoR2.CombatDirector.FixedUpdate += (orig, self) =>
            {
                if (NetworkServer.active) // server only
                {
                    combatTimer += Time.deltaTime;
                    if (combatTimer > Math.Max(5.5f - ((combatMonsterRating*4) / 60), 2f) ) // every 5 seconds or so we spawn something
                    {
                        combatTimer = 0f;

                        combatMonstersSpawned++; // handle difficulty scaling over time
                        if (combatMonstersSpawned > 9)
                        {
                            combatMonsterRating++;
                            combatMonstersSpawned = 6 - combatMonsterRating;
                        }

                        // Choose a random monster
                        int monster;
                        int monsterRating = Math.Min(combatMonsterRating, ridiculousMonsters.Length);
                        if (combatMonstersSpawned % 3 == 0) // every 3rd spawn we spawn a harder monster
                        {
                            monster = UnityEngine.Random.Range(monsterRating / 2, monsterRating); // spawn a harder monster
                        }
                        else
                        {
                            monster = UnityEngine.Random.Range(0, monsterRating / 2); // spawn an easier monster
                        }

                        string monsterName = ridiculousMonsters[monster];

                        if (UnityEngine.Random.Range(0f, 100f) < (float) combatMonsterRating-5)
                        {
                            Ridiculous.SpawnMonster(monsterName, (monster+1)*4, true); // spawn an elite
                        }
                        else
                        {
                            Ridiculous.SpawnMonster(monsterName, (monster+1)*4);
                        }
                    }
                }
            };

            // Fix the AI for custom maps
            On.EntityStates.AI.Walker.Combat.FixedUpdate += (orig, self) =>
            {
                if ((RoR2.CharacterAI.AISkillDriver)typeof(EntityStates.AI.Walker.Combat).GetField("dominantSkillDriver", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self))
                {
                    RoR2.CharacterAI.AISkillDriver skillDriver = (RoR2.CharacterAI.AISkillDriver)typeof(EntityStates.AI.Walker.Combat).GetField("dominantSkillDriver", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
                    skillDriver.ignoreNodeGraph = true;
                }
                orig(self);
            };

            // We don't need the difficulty slider anymore
            On.RoR2.UI.DifficultyBarController.Awake += (orig, self) =>
            {
                UnityEngine.Object.DestroyImmediate(self.transform.parent.gameObject);
            };

            // Don't use drop pods, they're too buggy
            On.RoR2.SceneDirector.Start += (orig, self) =>
            {
                Stage.instance.usePod = false;
                orig(self);
            };
            // Spawn at 0, 0, 0
            On.RoR2.Stage.GetPlayerSpawnTransform += (orig, self) =>
            {
                return new GameObject { transform = { position = Vector3.zero } }.transform;
            };

            // Track item pickups here
            On.RoR2.Chat.AddPickupMessage += (orig, body, pickupToken, pickupColor, pickupQuantity) =>
            {
                if (pickupToken == "ITEM_TREASURECACHE_NAME")
                {
                    body.gameObject.AddComponent<KeyEater>();
                }
                
                orig(body, pickupToken, pickupColor, pickupQuantity);
            };

            // We don't level up in this mod, so remove the code for it
            On.RoR2.UI.HUD.Awake += (orig, self) =>
            {
                orig(self);
                self.levelText.gameObject.transform.parent.gameObject.SetActive(false);
            };
            On.RoR2.UI.LevelText.SetDisplayData += (orig, self, _) => { };
            On.RoR2.UI.LevelText.Update += (orig, self) => { };
            On.RoR2.UI.LevelText.OnValidate += (orig, self) => { };

            // Ban items that don't work with Risk of Ridiculous
            On.RoR2.Run.BuildDropTable += (orig, self) =>
            {
                self.availableItems.RemoveItem(ItemIndex.WardOnLevel);
                self.availableItems.RemoveItem(ItemIndex.TreasureCache);
                self.availableItems.RemoveItem(ItemIndex.BossDamageBonus);
                self.availableItems.RemoveItem(ItemIndex.Firework);

                orig(self);
            };

            // Shrines are not typical shrines so we'll give them a cool effect to symbolize that
            On.RoR2.ShrineChanceBehavior.Start += (orig, self) =>
            {
                orig(self);
                Renderer shrine = self.GetComponentsInChildren<Renderer>()[0];
                Material[] mats = new Material[]
                {
                    Resources.Load<Material>("Materials/matIsFrozen"),
                    CharacterModel.ghostMaterial
                };
                shrine.materials = mats;
            };
            On.RoR2.ShrineRestackBehavior.Start += (orig, self) =>
            {
                orig(self);
                Renderer shrine = self.GetComponentsInChildren<Renderer>()[0];
                Material[] mats = new Material[]
                {
                    Resources.Load<Material>("Materials/matIsFrozen"),
                    CharacterModel.ghostMaterial
                };
                shrine.materials = mats;
            };

            // Overwrite the shrine restacking behavior
            On.RoR2.Inventory.ShrineRestackInventory += (orig, self, rng) =>
            {
                if (NetworkServer.active) // server only
                {
                    int numTier1 = self.GetTotalItemCountOfTier(ItemTier.Tier1);
                    int numTier2 = self.GetTotalItemCountOfTier(ItemTier.Tier2);
                    int numTier3 = self.GetTotalItemCountOfTier(ItemTier.Tier3);

                    // Reset all items
                    foreach (PickupIndex pickupIndex in Run.instance.availableTier1DropList)
                    {
                        self.ResetItem(pickupIndex.itemIndex);
                    }
                    foreach (PickupIndex pickupIndex in Run.instance.availableTier2DropList)
                    {
                        self.ResetItem(pickupIndex.itemIndex);
                    }
                    foreach (PickupIndex pickupIndex in Run.instance.availableTier3DropList)
                    {
                        self.ResetItem(pickupIndex.itemIndex);
                    }
                    self.ResetItem(ItemIndex.TreasureCache); // reset the key that isn't technically in the drop table
                    self.itemAcquisitionOrder.Clear();
                    self.SetDirtyBit(8u);

                    // Add new random items
                    for (int i = 0; i < numTier1; i++)
                    {
                        self.GiveItem(Run.instance.availableTier1DropList[rng.RangeInt(0, Run.instance.availableTier1DropList.Count - 1)].itemIndex);
                    }
                    for (int i = 0; i < numTier2; i++)
                    {
                        self.GiveItem(Run.instance.availableTier2DropList[rng.RangeInt(0, Run.instance.availableTier2DropList.Count - 1)].itemIndex);
                    }
                    for (int i = 0; i < numTier3; i++)
                    {
                        self.GiveItem(Run.instance.availableTier3DropList[rng.RangeInt(0, Run.instance.availableTier3DropList.Count - 1)].itemIndex);
                    }
                }
            };
            On.RoR2.ShrineRestackBehavior.Start += (orig, self) =>
            {
                orig(self);
                self.GetComponent<PurchaseInteraction>().costType = CostTypeIndex.Money;
            };

            // Chance shrine should never fail
            R2API.ItemDropAPI.DefaultShrineFailureWeight = 0f;
        }

        // Static method for spawning a new enemy
        public static void SpawnMonster(string monsterName, int netWorth, bool isElite = false)
        {
            if (NetworkServer.active) // server only
            {
                // Choose spawn location
                Vector3 spawnPosition = Vector3.zero;
                if (monsterName == "Jellyfish" || monsterName == "Wisp" || monsterName == "GreaterWisp" || monsterName == "Bell")
                {
                    spawnPosition = flyerSpawner[UnityEngine.Random.Range(1, flyerSpawner.Length)].position;
                }
                else if (monsterName == "HermitCrab")
                {
                    spawnPosition = crabSpawner[UnityEngine.Random.Range(1, crabSpawner.Length)].position;
                }
                else if (monsterName == "Vagrant")
                {
                    spawnPosition = vagrantSpawner[UnityEngine.Random.Range(1, vagrantSpawner.Length)].position;
                }
                else if (monsterName == "Titan" || monsterName == "LemurianBruiser")
                {
                    spawnPosition = bigSpawner[UnityEngine.Random.Range(1, bigSpawner.Length)].position;
                }
                else
                {
                    spawnPosition = basicSpawner[UnityEngine.Random.Range(1, basicSpawner.Length)].position;
                }

                GameObject masterPrefab = MasterCatalog.FindMasterPrefab(monsterName + "Master");
                GameObject bodyPrefab = BodyCatalog.FindBodyPrefab(monsterName + "Body");

                GameObject monster = Instantiate<GameObject>(masterPrefab, spawnPosition, Quaternion.identity);
                monster.AddComponent<MasterSuicideOnTimer>().lifeTimer = 300f; // 5 minute life timer just in case
                CharacterMaster master = monster.GetComponent<CharacterMaster>();

                if (isElite)
                {
                    if (combatMonstersSpawned % 2 == 0)
                    {
                        master.inventory.SetEquipmentIndex(EliteCatalog.GetEliteDef(EliteIndex.Fire).eliteEquipmentIndex);
                    }
                    else
                    {
                        master.inventory.SetEquipmentIndex(EliteCatalog.GetEliteDef(EliteIndex.Ice).eliteEquipmentIndex);
                    }
                    netWorth *= 2; // Elites are worth double
                }

                master.money = (uint)netWorth;

                NetworkServer.Spawn(monster);
                master.SpawnBody(bodyPrefab, spawnPosition, Quaternion.identity);
            }
        }
    }

    // Below we'll keep special monobehaviours for spawning items and unique events
    // ----------------------------------------------------------------------------
    public class TeddyDrop : MonoBehaviour
	{
		void Awake()
		{
            if (NetworkServer.active) // server only
            {
                PickupDropletController.CreatePickupDroplet(new PickupIndex(ItemIndex.Bear), this.transform.position, Vector3.zero);
            }
		}
		void Update()
		{
			UnityEngine.Object.DestroyImmediate(this.gameObject);
		}
	}
    // ----------------------------------------------------------------------------
    public class KeyDrop : MonoBehaviour
	{
		void Awake()
		{
            if (NetworkServer.active) // server only
            {
                PickupDropletController.CreatePickupDroplet(new PickupIndex(ItemIndex.TreasureCache), this.transform.position, Vector3.zero);
            }
		}
		void Update()
		{
			UnityEngine.Object.DestroyImmediate(this.gameObject);
		}
	}
    // ----------------------------------------------------------------------------
    public class KeyEater : MonoBehaviour
    {
        Vector3 keyEater;

        void Awake()
        {
            this.keyEater = GameObject.Find("keyhole easter egg").transform.position;
        }
        void Update()
        {
            if (Vector3.Distance(this.transform.position, this.keyEater) < 2f)
            {
                UnityEngine.Object.DestroyImmediate(GameObject.Find("keyhole easter egg"));
                Util.PlaySound("Play_UI_achievementUnlock", RoR2Application.instance.gameObject);
                UnityEngine.Component.DestroyImmediate(this);
            }
        }
    }
    // ----------------------------------------------------------------------------
    public class Pokeball : MonoBehaviour
	{
		Vector3 pokeballEater;

		void Awake()
		{
			this.pokeballEater = GameObject.Find("pokeball eater").transform.position;
		}
		void Update()
		{
			if(Vector3.Distance(this.transform.position, this.pokeballEater) < 1.5f)
			{
                PickupDropletController.CreatePickupDroplet(new PickupIndex(ItemIndex.ShockNearby), GameObject.Find("pokeball eater").transform.position, Vector3.zero);
                UnityEngine.Object.DestroyImmediate(this.gameObject);
			}
		}
    }
    // ----------------------------------------------------------------------------
    public class ChanceShrine : MonoBehaviour
    {
        void Awake()
        {
            if (NetworkServer.active)
            {
                string shrinePrefab = "prefabs/networkedobjects/ShrineChance";
                GameObject shrine = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>(shrinePrefab), this.transform.position, this.transform.rotation);
                RoR2.PurchaseInteraction purchaseInteraction = shrine.GetComponent<RoR2.PurchaseInteraction>();

                RoR2.ShrineChanceBehavior shrineBehavior = shrine.GetComponent<RoR2.ShrineChanceBehavior>();
                shrineBehavior.maxPurchaseCount = 999;
                shrineBehavior.costMultiplierPerPurchase = 1.2f;

                purchaseInteraction.cost = 25;
                NetworkServer.Spawn(shrine);
            }
            UnityEngine.Component.DestroyImmediate(this);
        }
    }
    // ----------------------------------------------------------------------------
    public class OrderShrine : MonoBehaviour
    {
        void Awake()
        {
            if (NetworkServer.active)
            {
                string shrinePrefab = "prefabs/networkedobjects/ShrineRestack";
                GameObject shrine = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>(shrinePrefab), this.transform.position, this.transform.rotation);
                RoR2.PurchaseInteraction purchaseInteraction = shrine.GetComponent<RoR2.PurchaseInteraction>();

                RoR2.ShrineRestackBehavior shrineBehavior = shrine.GetComponent<RoR2.ShrineRestackBehavior>();
                shrineBehavior.maxPurchaseCount = 999;
                shrineBehavior.costMultiplierPerPurchase = 1.2f;

                purchaseInteraction.costType = CostTypeIndex.Money;
                purchaseInteraction.cost = 25;
                NetworkServer.Spawn(shrine);
            }
            UnityEngine.Component.DestroyImmediate(this);
        }
    }
    // ----------------------------------------------------------------------------
    public class ChestDrop : MonoBehaviour
    {
        void Awake()
        {
            if (NetworkServer.active) // server only
            {
                GameObject chest = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("prefabs/networkedobjects/chest2"), this.transform.position, Quaternion.identity);
                NetworkServer.Spawn(chest);
            }
        }
        void Update()
        {
            UnityEngine.Component.DestroyImmediate(this);
        }
    }
    // ----------------------------------------------------------------------------
    public class ChestLakitu : MonoBehaviour
    {
        void Awake()
        {
            if (NetworkServer.active) // server only
            {
                GameObject chest = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("prefabs/networkedobjects/chest1"), this.transform.position - new Vector3(0f, 1.3f, 0f), Quaternion.identity);
                chest.transform.parent = this.gameObject.transform;
                NetworkServer.Spawn(chest);
            }
        }
        void Update()
        {
            UnityEngine.Component.DestroyImmediate(this);
        }
    }
    // ----------------------------------------------------------------------------
}