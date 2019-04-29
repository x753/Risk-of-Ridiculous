using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Ridiculousness
{
    [BepInPlugin("com.753.RiskOfRidiculous", "Risk of Ridiculous", "0.9")]

    public class Ridiculous : BaseUnityPlugin
    {
        // Static variables to be used wherever necessary
        public static AssetBundle ridiculousAssetBundle;
        public static GameObject ridiculousMap;

        public static string[] ridiculousMonsters;

        public static float combatTimer = 0f;
		public static int combatMonsterRating = 0;
		public static int combatMonstersSpawned = 0;

		public static Transform[] basicSpawner;
		public static Transform[] flyerSpawner;
		public static Transform[] crabSpawner;
		public static Transform[] vagrantSpawner;
		public static Transform[] bigSpawner;

        public void Awake()
        {
            // Load in our map asset
            ridiculousAssetBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/modmap");
			ridiculousMap = ridiculousAssetBundle.LoadAsset<GameObject>("Assets/modmap.prefab");

            // Create an array of monsters we're able to spawn in our custom map
            ridiculousMonsters = new string[]
			{
				"Beetle",
				"Jellyfish",
				"Wisp",
				"Lemurian",
				"Golem",
				"Bell",
				"HermitCrab",
				"GreaterWisp",
				"Vagrant",
				"BeetleQueen",
				"Titan"
			};

            // The only stage we should load is testscene
            On.RoR2.Run.PickNextStageScene += (orig, self, choices) =>
            {
                self.nextStageScene = new SceneField("testscene");
            };

            // Initialize the map on start of match
            On.RoR2.Stage.Start += (orig, self) =>
            {
                orig(self);

                UnityEngine.Object.Instantiate<GameObject>(ridiculousMap, Vector3.zero, Quaternion.identity);

			    // Destroy pre-existing garbage in the scene
			    UnityEngine.Object.DestroyImmediate(GameObject.Find("Directional Light (SUN)"));
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
			    GameObject.Find("pokeball").AddComponent<Pokeball>();
			    GameObject.Find("teddy easter egg 1").AddComponent<TeddyDrop>();
			    GameObject.Find("teddy easter egg 2").AddComponent<TeddyDrop>();
			    GameObject.Find("teddy easter egg 3").AddComponent<TeddyDrop>();
			    GameObject.Find("key for keyhole").AddComponent<KeyDrop>();

                // Locate monster spawn positions
                basicSpawner = GameObject.Find("basic spawner").GetComponentsInChildren<Transform>();
				flyerSpawner = GameObject.Find("flyer spawner").GetComponentsInChildren<Transform>();
				crabSpawner = GameObject.Find("crab spawner").GetComponentsInChildren<Transform>();
				vagrantSpawner = GameObject.Find("vagrant spawner").GetComponentsInChildren<Transform>();
				bigSpawner = GameObject.Find("big spawner").GetComponentsInChildren<Transform>();
				combatMonsterRating = 4; // start off only able to spawn the first 4 monsters
            };

            // Override enemy spawning behavior
            On.RoR2.CombatDirector.FixedUpdate += (orig, self) =>
            {
                combatTimer += Time.deltaTime;
			    if(combatTimer > 5f) // every 5 or so seconds we try to spawn something
			    {
				    combatTimer = 0f;
                    
				    combatMonstersSpawned++; // handle difficulty scaling over time
                    if(combatMonstersSpawned > 9)
				    {
                        if(combatMonsterRating <= ridiculousMonsters.Length)
                        {
                            combatMonsterRating++;
                        }
					    combatMonstersSpawned = 0 - combatMonsterRating;
				    }

                    // Choose a random monster
				    string monsterName = ridiculousMonsters[UnityEngine.Random.Range(0, combatMonsterRating)];

                    Ridiculous.SpawnMonster(monsterName);
                }
            };
        }

        // Static method for spawning a new enemy
        public static void SpawnMonster(string monsterName)
        {
            // Choose spawn location
    		Vector3 spawnPosition = Vector3.zero;
			if(monsterName == "Jellyfish" || monsterName == "Wisp" || monsterName == "GreaterWisp" || monsterName == "Bell")
			{
				spawnPosition = flyerSpawner[UnityEngine.Random.Range(0,flyerSpawner.Length)].position;
			}
			else if(monsterName == "HermitCrab")
			{
				spawnPosition = crabSpawner[UnityEngine.Random.Range(0,crabSpawner.Length)].position;
			}
			else if(monsterName == "Vagrant")
			{
				spawnPosition = vagrantSpawner[UnityEngine.Random.Range(0,vagrantSpawner.Length)].position;
			}
			else if(monsterName == "BeetleQueen" || monsterName == "Titan")
			{
				spawnPosition = bigSpawner[UnityEngine.Random.Range(0,bigSpawner.Length)].position;
			}
			else
			{
				spawnPosition = basicSpawner[UnityEngine.Random.Range(0,basicSpawner.Length)].position;
			}

			GameObject masterPrefab = MasterCatalog.FindMasterPrefab(monsterName + "Master");
			GameObject bodyPrefab = BodyCatalog.FindBodyPrefab(monsterName + "Body");

			GameObject monster = Instantiate<GameObject>(masterPrefab, spawnPosition, Quaternion.identity);
			monster.AddComponent<MasterSuicideOnTimer>().lifeTimer = 300f; // 5 minute life timer just in case
			CharacterMaster master = monster.GetComponent<CharacterMaster>();
			master.money = 5; // every monster is worth $5 when killed
			NetworkServer.Spawn(monster);
			master.SpawnBody(bodyPrefab, spawnPosition, Quaternion.identity);
        }
    }

    // Below we'll keep special monobehaviours for spawning items and unique events
    // ----------------------------------------------------------------------------
    public class TeddyDrop : MonoBehaviour
	{
		void Awake()
		{
			PickupDropletController.CreatePickupDroplet(new PickupIndex(ItemIndex.Bear), this.transform.position, Vector3.zero);
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
			PickupDropletController.CreatePickupDroplet(new PickupIndex(ItemIndex.TreasureCache), this.transform.position, Vector3.zero);
		}
		void Update()
		{
			UnityEngine.Object.DestroyImmediate(this.gameObject);
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
				UnityEngine.Object.DestroyImmediate(this.gameObject);
				PickupDropletController.CreatePickupDroplet(new PickupIndex(ItemIndex.ShockNearby), pokeballEater, Vector3.zero);
			}
		}
	}
    // ----------------------------------------------------------------------------
}