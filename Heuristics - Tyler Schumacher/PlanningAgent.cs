using System;
using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.GameElements;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////
/// This is the Moron Agent
/////////////////////////////////////////////////////////////////////////////

namespace GameManager
{
    /// <summary>
    /// Takes an action
    /// </summary>
    public delegate void TakeAction();

    ///<summary>Planning Agent is the over-head planner that decided where
    /// individual units go and what tasks they perform.  Low-level 
    /// AI is handled by other classes (like pathfinding).
    ///</summary> 
    public class PlanningAgent : Agent
    {
        /// <summary>
        /// Labels of each state
        /// </summary>
        enum State
		{
            Build,
            Attack,
            Win
		};

        private const int MAX_BASE = 1;
        private const int MAX_REFINERY = 3;
        private const int MAX_BARRACKS = 3;
        private const int MAX_WORKERS = 15;
        private const int MAX_ARCHERS = 20;
        private const int MAX_SOLDIERS = 10;

        #region Private Data

        ///////////////////////////////////////////////////////////////////////
        // Handy short-cuts for pulling all of the relevant data that you
        // might use for each decision.  Feel free to add your own.
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The enemy's agent number
        /// </summary>
        private int enemyAgentNbr { get; set; }

        /// <summary>
        /// My primary mine number
        /// </summary>
        private int mainMineNbr { get; set; }

        /// <summary>
        /// My primary base number
        /// </summary>
        private int mainBaseNbr { get; set; }

        /// <summary>
        /// List of all the mines on the map
        /// </summary>
        private List<int> mines { get; set; }

        /// <summary>
        /// List of all of my workers
        /// </summary>
        private List<int> myWorkers { get; set; }

        /// <summary>
        /// List of all of my soldiers
        /// </summary>
        private List<int> mySoldiers { get; set; }

        /// <summary>
        /// List of all of my archers
        /// </summary>
        private List<int> myArchers { get; set; }

        /// <summary>
        /// List of all of my bases
        /// </summary>
        private List<int> myBases { get; set; }

        /// <summary>
        /// List of all of my barracks
        /// </summary>
        private List<int> myBarracks { get; set; }

        /// <summary>
        /// List of all of my refineries
        /// </summary>
        private List<int> myRefineries { get; set; }

        /// <summary>
        /// List of the enemy's workers
        /// </summary>
        private List<int> enemyWorkers { get; set; }

        /// <summary>
        /// List of the enemy's soldiers
        /// </summary>
        private List<int> enemySoldiers { get; set; }

        /// <summary>
        /// List of enemy's archers
        /// </summary>
        private List<int> enemyArchers { get; set; }

        /// <summary>
        /// List of the enemy's bases
        /// </summary>
        private List<int> enemyBases { get; set; }

        /// <summary>
        /// List of the enemy's barracks
        /// </summary>
        private List<int> enemyBarracks { get; set; }

        /// <summary>
        /// List of the enemy's refineries
        /// </summary>
        private List<int> enemyRefineries { get; set; }

        /// <summary>
        /// List of the possible build positions for a 3x3 unit
        /// </summary>
        private List<Vector3Int> buildPositions { get; set; }

        /// <summary>
        /// Finds all of the possible build locations for a specific UnitType.
        /// Currently, all structures are 3x3, so these positions can be reused
        /// for all structures (Base, Barracks, Refinery)
        /// Run this once at the beginning of the game and have a list of
        /// locations that you can use to reduce later computation.  When you
        /// need a location for a build-site, simply pull one off of this list,
        /// determine if it is still buildable, determine if you want to use it
        /// (perhaps it is too far away or too close or not close enough to a mine),
        /// and then simply remove it from the list and build on it!
        /// This method is called from the Awake() method to run only once at the
        /// beginning of the game.
        /// </summary>
        /// <param name="unitType">the type of unit you want to build</param>
        public void FindProspectiveBuildPositions(UnitType unitType)
        {
            // For the entire map
            for (int i = 0; i < GameManager.Instance.MapSize.x; ++i)
            {
                for (int j = 0; j < GameManager.Instance.MapSize.y; ++j)
                {
                    // Construct a new point near gridPosition
                    Vector3Int testGridPosition = new Vector3Int(i, j, 0);

                    // Test if that position can be used to build the unit
                    if (Utility.IsValidGridLocation(testGridPosition)
                        && GameManager.Instance.IsBoundedAreaBuildable(unitType, testGridPosition))
                    {
                        // If this position is buildable, add it to the list
                        buildPositions.Add(testGridPosition);
                    }
                }
            }
        }

        /// <summary>
        /// Build a building
        /// </summary>
        /// <param name="unitType"></param>
        public void BuildBuilding(UnitType unitType)
        {
            // For each worker
            foreach (int worker in myWorkers)
            {
                // Grab the unit we need for this function
                Unit unit = GameManager.Instance.GetUnit(worker);

                // Make sure this unit actually exists and we have enough gold
                if (unit != null && Gold >= Constants.COST[unitType])
                {
                    // Find the closest build position to this worker's position (DUMB) and 
                    // build the base there
                    foreach (Vector3Int toBuild in buildPositions)
                    {
                        if (GameManager.Instance.IsBoundedAreaBuildable(unitType, toBuild))
                        {
                            Build(unit, toBuild, unitType);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attack the enemy
        /// </summary>
        /// <param name="myTroops"></param>
        public void AttackEnemy(List<int> myTroops)
        {
            // For each of my troops in this collection
            foreach (int troopNbr in myTroops)
            {
                // If this troop is idle, give him something to attack
                Unit troopUnit = GameManager.Instance.GetUnit(troopNbr);
                if (troopUnit.CurrentAction == UnitAction.IDLE)
                {
                    // If there are archers to attack
                    if (enemyArchers.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyArchers[UnityEngine.Random.Range(0, enemyArchers.Count)]));
                    }
                    // If there are soldiers to attack
                    else if (enemySoldiers.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemySoldiers[UnityEngine.Random.Range(0, enemySoldiers.Count)]));
                    }
                    // If there are workers to attack
                    else if (enemyWorkers.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyWorkers[UnityEngine.Random.Range(0, enemyWorkers.Count)]));
                    }
                    // If there are bases to attack
                    else if (enemyBases.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyBases[UnityEngine.Random.Range(0, enemyBases.Count)]));
                    }
                    // If there are barracks to attack
                    else if (enemyBarracks.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyBarracks[UnityEngine.Random.Range(0, enemyBarracks.Count)]));
                    }
                    // If there are refineries to attack
                    else if (enemyRefineries.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyRefineries[UnityEngine.Random.Range(0, enemyRefineries.Count)]));
                    }
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called at the end of each round before remaining units are
        /// destroyed to allow the agent to observe the "win/loss" state
        /// </summary>
        public override void Learn()
        {
            Debug.Log("PlanningAgent::Learn");
        }

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds. 
        /// </summary>
        public override void InitializeMatch()
        {
	        Debug.Log("Moron's: " + AgentName);
            Debug.Log("PlanningAgent::InitializeMatch");
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// There are multiple rounds in a single match between two agents.
        /// </summary>
        public override void InitializeRound()
        {
            Debug.Log("PlanningAgent::InitializeRound");
            buildPositions = new List<Vector3Int>();

            FindProspectiveBuildPositions(UnitType.BASE);

            // Set the main mine and base to "non-existent"
            mainMineNbr = -1;
            mainBaseNbr = -1;

            // Initialize all of the unit lists
            mines = new List<int>();

            myWorkers = new List<int>();
            mySoldiers = new List<int>();
            myArchers = new List<int>();
            myBases = new List<int>();
            myBarracks = new List<int>();
            myRefineries = new List<int>();

            enemyWorkers = new List<int>();
            enemySoldiers = new List<int>();
            enemyArchers = new List<int>();
            enemyBases = new List<int>();
            enemyBarracks = new List<int>();
            enemyRefineries = new List<int>();
        }

        /// <summary>
        /// Updates the game state for the Agent - called once per frame for GameManager
        /// Pulls all of the agents from the game and identifies who they belong to
        /// </summary>
        public void UpdateGameState()
        {
            // Update the common resources
            mines = GameManager.Instance.GetUnitNbrsOfType(UnitType.MINE);

            // Update all of my unitNbrs
            myWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, AgentNbr);
            mySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, AgentNbr);
            myArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, AgentNbr);
            myBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, AgentNbr);
            myBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, AgentNbr);
            myRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, AgentNbr);

            // Update the enemy agents & unitNbrs
            List<int> enemyAgentNbrs = GameManager.Instance.GetEnemyAgentNbrs(AgentNbr);
            if (enemyAgentNbrs.Any())
            {
                enemyAgentNbr = enemyAgentNbrs[0];
                enemyWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, enemyAgentNbr);
                enemySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, enemyAgentNbr);
                enemyArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, enemyAgentNbr);
                enemyBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, enemyAgentNbr);
                enemyBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, enemyAgentNbr);
                enemyRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, enemyAgentNbr);
            }
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update()
        {
            Debug.Log("This shows that the code has been edited!");

            UpdateGameState();

            // Delegates to take actions
            TakeAction buildWorkers = BuildWorkers;
            TakeAction buildArchers = BuildArcher;
            TakeAction buildSoldiers = BuildSoldier;
            TakeAction buildBase = BuildBase;
            TakeAction buildBarracks = BuildBarracks;
            TakeAction buildRefinery = BuildRefinery;
            TakeAction collect = CollectWorkers;
            TakeAction attackArchers = ArcherAttack;
            TakeAction attackSoldiers = SoldierAttack;
            TakeAction attackEverything = DestroyBuildings;

            TakeAction[] takeActions = { buildBase, buildBarracks, buildRefinery, buildArchers, buildSoldiers, buildWorkers, collect, attackArchers, attackSoldiers, attackEverything };

            // Float variables
            float BuildingBase;
            float BuildingBarracks;
            float BuildingRefineries;
            float BuildingArchers;
            float BuildingSoldiers;
            float BuildingWorkers;
            float GatherGold;
            float AttackWithArchers;
            float AttackWithSoldiers;
            float AttackEverything;

            float highestNumber = 0f;
            int highestIndex = 0;
            float currentNumber = 0f;

            // Picks a state for the planning agent
            State state = State.Build;

            if (enemyArchers.Count == 0 && enemySoldiers.Count == 0 && enemyWorkers.Count == 0)
            {
                state = State.Win;
            }
            else if (Gold >= 2000)
            {
                state = State.Build;
            }
            else if (myArchers.Count > 7 || mySoldiers.Count >= 10)
            {
                state = State.Attack;
            }

            switch (state)
            {
                case State.Attack: // Attacks the enemies
                    Debug.Log("<color=blue>State = Attack </color>AsseBundle");
                    // Heuristics 
                    BuildingBase = Mathf.Clamp(1 - myBases.Count, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.BASE] - 1), 0, 1);
                    BuildingBarracks = Mathf.Clamp((myBarracks.Count - enemyBarracks.Count) - 3, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.BARRACKS] - 1), 0, 1);
                    BuildingRefineries = Mathf.Clamp(1 - myRefineries.Count, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.REFINERY] - 1), 0, 1);
                    BuildingArchers = Mathf.Clamp((enemyArchers.Count - myArchers.Count) - 4, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.ARCHER] - 1), 0, 1);
                    BuildingSoldiers = Mathf.Clamp((enemySoldiers.Count - mySoldiers.Count) - 4, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.SOLDIER] - 1), 0, 1);
                    BuildingWorkers = Mathf.Clamp(myWorkers.Count - 4, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.WORKER] - 1), 0, 1);
                    GatherGold = Mathf.Clamp(150 - Gold, 0, 1);
                    AttackWithArchers = Mathf.Clamp((enemyWorkers.Count + enemyArchers.Count + enemySoldiers.Count) - 4, 0, 1);
                    AttackWithSoldiers = Mathf.Clamp((enemySoldiers.Count + enemyArchers.Count + enemyWorkers.Count) - 4, 0, 1);
                    AttackEverything = Mathf.Clamp(enemyAgentNbr - (enemyArchers.Count + enemyWorkers.Count + enemySoldiers.Count), 0, 1) * Mathf.Clamp(enemyArchers.Count + enemySoldiers.Count + enemyWorkers.Count, 0, 1);

                    float[] action = { BuildingBase, BuildingBarracks, BuildingRefineries, BuildingArchers, BuildingSoldiers, BuildingWorkers, GatherGold, AttackWithArchers, AttackWithSoldiers, AttackEverything };

                    // Takes the action
                    if (action != null && takeActions != null)
                    {
                        for (int i = 0; i < action.Length; i++)
                        {
                            currentNumber = action[i];
                            if (currentNumber > highestNumber)
                            {
                                highestNumber = currentNumber;
                                highestIndex = i;
                                takeActions[highestIndex].DynamicInvoke();
                            }
                        }
                    }

                    Array.Clear(action, 0, action.Length);
                    Array.Clear(takeActions, 0, takeActions.Length);
                    break;
                case State.Build: // Builds the necessary elements
                    Debug.Log("<color=blue>State = Build </color>AssetBundle");
                    // Heuristics 
                    BuildingBase = Mathf.Clamp(1 - myBases.Count, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.BASE] - 1), 0, 1);
                    BuildingBarracks = Mathf.Clamp((enemyBarracks.Count - myBarracks.Count) + 2, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.BARRACKS] - 1), 0, 1);
                    BuildingRefineries = Mathf.Clamp(3 - myRefineries.Count, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.REFINERY] - 1), 0, 1);
                    BuildingArchers = Mathf.Clamp(enemyArchers.Count * 2, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.ARCHER] - 1), 0, 1);
                    BuildingSoldiers = Mathf.Clamp(mySoldiers.Count * 1, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.SOLDIER] - 1), 0, 1);
                    BuildingWorkers = Mathf.Clamp((enemyBases.Count - myWorkers.Count) + 5, 0, 1) * Mathf.Clamp(Gold - (Constants.COST[UnitType.WORKER] - 1), 0, 1);
                    GatherGold = Mathf.Clamp(1500 - Gold, 0, 1);
                    AttackWithArchers = Mathf.Clamp(myArchers.Count - 10, 0, 1);
                    AttackWithSoldiers = Mathf.Clamp(mySoldiers.Count - 10, 0, 1);
                    AttackEverything = Mathf.Clamp(enemyAgentNbr - (enemyArchers.Count + enemyWorkers.Count + enemySoldiers.Count), 0, 1) * Mathf.Clamp(enemyArchers.Count + enemySoldiers.Count + enemyWorkers.Count, 0, 1);

                    float[] action1 = { BuildingBase, BuildingBarracks, BuildingRefineries, BuildingArchers, BuildingSoldiers, BuildingWorkers, GatherGold, AttackWithArchers, AttackWithSoldiers, AttackEverything };

                    // Takes the action
                    
                    if (action1 != null && takeActions != null)
                    {
                        for (int i = 0; i < action1.Length; i++)
                        {
                            currentNumber = action1[i];
                            if (currentNumber > highestNumber)
                            {
                                highestNumber = currentNumber;
                                highestIndex = i;
                                takeActions[highestIndex].DynamicInvoke();
                            }
                        }
                    }

                    Array.Clear(action1, 0, action1.Length);
                    Array.Clear(takeActions, 0, takeActions.Length);
                    break;
                case State.Win: // Destroys the enemy's buildings
                    Debug.Log("<color=blue>State = Win </color>AssetBundle");
                    // Heuristics 
                    BuildingBase = Mathf.Clamp(0, 0, 1);
                    BuildingBarracks = Mathf.Clamp(0, 0, 1);
                    BuildingRefineries = Mathf.Clamp(0, 0, 1);
                    BuildingArchers = Mathf.Clamp(0, 0, 1);
                    BuildingSoldiers = Mathf.Clamp(0, 0, 1);
                    BuildingWorkers = Mathf.Clamp(0, 0, 1);
                    GatherGold = Mathf.Clamp(0, 0, 1);
                    AttackWithArchers = Mathf.Clamp(0, 0, 1);
                    AttackWithSoldiers = Mathf.Clamp(0, 0, 1);
                    AttackEverything = Mathf.Clamp(1, 0, 1);

                    float[] action2 = { BuildingBase, BuildingBarracks, BuildingRefineries, BuildingArchers, BuildingSoldiers, BuildingWorkers, GatherGold, AttackWithArchers, AttackWithSoldiers, AttackEverything };

                    // Takes the action
                    if (action2 != null && takeActions != null)
                    {
                        for (int i = 0; i < action2.Length; i++)
                        {
                            currentNumber = action2[i];
                            if (currentNumber > highestNumber)
                            {
                                highestNumber = currentNumber;
                                highestIndex = i;
                                takeActions[highestIndex].DynamicInvoke();
                            }
                        }
                    }

                    Array.Clear(action2, 0, action2.Length);
                    Array.Clear(takeActions, 0, takeActions.Length);
                    break;
            }           
        }

        #endregion

        #region Attack

        /// <summary>
        /// Archers attack
        /// </summary>
        public void ArcherAttack()
		{
            Debug.Log("<color=orange>Archer Attacks </color>AssetBundle");
            /*foreach (int worker in enemyWorkers)
            {
                Unit unitEW = GameManager.Instance.GetUnit(enemyWorkers.Count);
                Unit unitEA = GameManager.Instance.GetUnit(enemyArchers.Count);
                Unit unitES = GameManager.Instance.GetUnit(enemySoldiers.Count);
                foreach (int archer in myArchers)
                {
                    Unit unit = GameManager.Instance.GetUnit(archer);
                    if (unit != null)
                    {
                        AttackEnemy(myArchers);
                        //Attack(unit, unitEW);
                        //Attack(unit, unitEA);
                        //Attack(unit, unitES);
                    }
                }
            }*/
            
            foreach (int enemyWorker in enemyWorkers)
            {
                Unit unitEW = GameManager.Instance.GetUnit(enemyWorker);
                foreach (int archer in myArchers)
                {
                    Unit unit = GameManager.Instance.GetUnit(archer);

                    if (unit != null && unitEW != null && unit.CurrentAction == UnitAction.IDLE)
                    {
                        Attack(unit, unitEW);
                    }
                }
            }

            foreach (int enemyArcher in enemyArchers)
            {
                Unit unitEA = GameManager.Instance.GetUnit(enemyArcher);
                foreach (int archer in myArchers)
                {
                    Unit unit = GameManager.Instance.GetUnit(archer);

                    if (unit != null && unitEA != null && unit.CurrentAction == UnitAction.IDLE)
                    {
                        Attack(unit, unitEA);
                    }
                }
            }

            foreach (int enemySoldier in enemySoldiers)
            {
                Unit unitES = GameManager.Instance.GetUnit(enemySoldier);
                foreach (int archer in myArchers)
                {
                    Unit unit = GameManager.Instance.GetUnit(archer);

                    if (unit != null && unitES != null && unit.CurrentAction == UnitAction.IDLE)
                    {
                        Attack(unit, unitES);
                    }
                }
            }

            /*
            foreach (int enemyArcher in enemyArchers)
            {
                Unit unitEA = GameManager.Instance.GetUnit(enemyArcher);

                foreach (int enemySoldier in enemySoldiers)
                {
                    Unit unitES = GameManager.Instance.GetUnit(enemySoldier);

                    if (unit != null && unit.CanAttack)
                    {
                        unit.CurrentAction.Equals(UnitAction.ATTACK);

                            

                        if (enemyArchers.Count == 0)
                        {
                            Attack(unit, unitES);
                            Attack(unit, unitEA);
                        }
                    }
                }
            }*/
        }

        /// <summary>
        /// Soliders attack
        /// </summary>
        public void SoldierAttack()
		{
            Debug.Log("<color=orange>Soldier Attacks </color>AssetBundle");

            /*foreach (int worker in enemyWorkers)
            {
                Unit unitEW = GameManager.Instance.GetUnit(enemyWorkers.Count);
                Unit unitEA = GameManager.Instance.GetUnit(enemyArchers.Count);
                Unit unitES = GameManager.Instance.GetUnit(enemySoldiers.Count);
                foreach (int soldier in mySoldiers)
                {
                    Unit unit = GameManager.Instance.GetUnit(soldier);
                    if (unit != null)
                    {
                        AttackEnemy(mySoldiers);
                        //Attack(unit, unitEW);
                        //Attack(unit, unitEA);
                        //Attack(unit, unitES);
                    }
                }
            }*/

            foreach (int enemyWorker in enemyWorkers)
            {
                Unit unitEW = GameManager.Instance.GetUnit(enemyWorker);
                foreach (int soldier in mySoldiers)
                {
                    Unit unit = GameManager.Instance.GetUnit(soldier);

                    if (unit != null && unitEW != null && unit.CurrentAction == UnitAction.IDLE)
                    {
                        Attack(unit, unitEW);
                    }
                }
            }

            foreach (int enemyArcher in enemyArchers)
            {
                Unit unitEA = GameManager.Instance.GetUnit(enemyArcher);
                foreach (int soldier in mySoldiers)
                {
                    Unit unit = GameManager.Instance.GetUnit(soldier);

                    if (unit != null && unitEA != null && unit.CurrentAction == UnitAction.IDLE)
                    {
                        Attack(unit, unitEA);
                    }
                }
            }

            foreach (int enemySoldier in enemySoldiers)
            {
                Unit unitES = GameManager.Instance.GetUnit(enemySoldier);
                foreach (int soldier in mySoldiers)
                {
                    Unit unit = GameManager.Instance.GetUnit(soldier);

                    if (unit != null && unitES != null && unit.CurrentAction == UnitAction.IDLE)
                    {
                        Attack(unit, unitES);
                    }
                }
            }

            /*foreach (int soldier in mySoldiers)
            {
                Unit unit = GameManager.Instance.GetUnit(soldier);

                foreach (int enemyArcher in enemyArchers)
                {
                    Unit unitEA = GameManager.Instance.GetUnit(enemyArcher);

                    foreach (int enemySoldier in enemySoldiers)
                    {
                        Unit unitES = GameManager.Instance.GetUnit(enemySoldier);

                        if (unit != null && unit.CanAttack)
                        {
                            unit.CurrentAction.Equals(UnitAction.ATTACK);

                            Attack(unit, unitEA);
    
                            if (enemyArchers.Count == 0)
                            {
                                Attack(unit, unitES);
                            }
                        }
                    }
                }
            */
        }

        #endregion

        #region Build

        /// <summary>
        /// Builds the main base
        /// </summary>
        public void BuildBase()
		{
            Debug.Log("<color=yellow>Built base </color>AssetBundle");

            foreach (int workers in myWorkers)
			{
                foreach (int miners in mines)
                {
                    Unit unit = GameManager.Instance.GetUnit(workers);
                    Unit minesUnit = GameManager.Instance.GetUnit(miners);
                    Vector3Int position = minesUnit.GridPosition;

                    if (unit != null)
                    {
                        float shortestDistance = 9999f;
                        Vector3Int buildLocation = new Vector3Int(0, 0, 0);

                        foreach (Vector3Int toBuild in buildPositions)
                        {
                            if (GameManager.Instance.IsBoundedAreaBuildable(UnitType.BASE, toBuild))
                            {
                                Vector3Int number = toBuild - position;
                                float distance = number.sqrMagnitude;

                                if (distance < shortestDistance)
                                {
                                    shortestDistance = distance;
                                    buildLocation = toBuild;
                                }
                            }
                        }
                        Build(unit, buildLocation, UnitType.BASE);
                    }
                }
			}
		}
        
        /// <summary>
        /// Builds the refinery
        /// </summary>
        public void BuildRefinery()
		{
            Debug.Log("<color=yellow>Built refinery </color>AssetBundle");
            foreach (int workers in myWorkers)
            {
                foreach (int bases in myBases)
                {
                    Unit unit = GameManager.Instance.GetUnit(workers);
                    Unit basesUnit = GameManager.Instance.GetUnit(bases);
                    Vector3Int position = basesUnit.GridPosition;

                    if (unit != null)
                    {
                        float shortestDistance = 9999f;
                        Vector3Int buildLocation = new Vector3Int(0, 0, 0);

                        foreach (Vector3Int toBuild in buildPositions)
                        {
                            if (GameManager.Instance.IsBoundedAreaBuildable(UnitType.REFINERY, toBuild))
                            {
                                Vector3Int number = toBuild - position;
                                float distance = number.sqrMagnitude;

                                if (distance < shortestDistance)
                                {
                                    shortestDistance = distance;
                                    buildLocation = toBuild;
                                }
                            }
                        }
                        Build(unit, buildLocation, UnitType.REFINERY);
                    }
                }
            }
        }

        /// <summary>
        /// Builds the barracks
        /// </summary>
        public void BuildBarracks()
		{
            Debug.Log("<color=yellow>Built barracks </color>AssetBundle");
            foreach (int workers in myWorkers)
            {
                foreach (int bases in myBases)
                {
                    Unit unit = GameManager.Instance.GetUnit(workers);
                    Unit baseUnit = GameManager.Instance.GetUnit(bases);
                    Vector3Int position = baseUnit.GridPosition;

                    if (unit != null)
                    {
                        float shortestDistance = 9999f;
                        Vector3Int buildLocation = new Vector3Int(0, 0, 0);

                        foreach (Vector3Int toBuild in buildPositions)
                        {
                            if (GameManager.Instance.IsBoundedAreaBuildable(UnitType.BARRACKS, toBuild))
                            {
                                Vector3Int number = toBuild - position;
                                float distance = number.sqrMagnitude;

                                if (distance < shortestDistance)
                                {
                                    shortestDistance = distance;
                                    buildLocation = toBuild;
                                }
                            }
                        }
                        Build(unit, buildLocation, UnitType.BARRACKS);
                    }
                }
            }
        }

        /// <summary>
        /// Workers go out and collect
        /// </summary>
        public void CollectWorkers()
        {
            Debug.Log("<color=yellow>Workers Collect </color>AssetBundle");
            // For each worker
            foreach (int worker in myWorkers)
            {
                // Grab the unit we need for this function
                Unit unit = GameManager.Instance.GetUnit(worker);

                // Make sure this unit actually exists and is idle
                if (unit != null && unit.CurrentAction == UnitAction.IDLE && mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    // Grab the mine
                    Unit mineUnit = GameManager.Instance.GetUnit(mainMineNbr);
                    Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);
                    if (mineUnit != null && baseUnit != null && mineUnit.Health > 0)
                    {
                        Gather(unit, mineUnit, baseUnit);
                    }
                }
            }
        }

        /// <summary>
        /// Build workers
        /// </summary>
        public void BuildWorkers()
		{
            Debug.Log("<color=yellow>Built worker </color>AssetBundle");
            if (myWorkers.Count <= MAX_WORKERS)
			{
                if (mines.Count > 0)
                {
                    mainMineNbr = mines[0];
                }
                else
                {
                    mainMineNbr = -1;
                }

                // If we have at least one base, assume the first one is our "main" base
                if (myBases.Count > 0)
                {
                    mainBaseNbr = myBases[0];
                    Debug.Log("BaseNbr " + mainBaseNbr);
                    Debug.Log("MineNbr " + mainMineNbr);
                }

                // For each base, determine if it should train a worker
                foreach (int baseNbr in myBases)
                {
                    // Get the base unit
                    Unit baseUnit = GameManager.Instance.GetUnit(baseNbr);

                    // If the base exists, is idle, we need a worker, and we have gold
                    if (baseUnit != null && baseUnit.IsBuilt
                                         && baseUnit.CurrentAction == UnitAction.IDLE
                                         && Gold >= Constants.COST[UnitType.WORKER])
                    {
                        Train(baseUnit, UnitType.WORKER);
                    }
                }
            }
        }

        /// <summary>
        /// Builds archers
        /// </summary>
        public void BuildArcher()
		{
            Debug.Log("<color=yellow>Built archer </color>AssetBundle");
            // For each barracks, determine if it should train a soldier or an archer
            foreach (int barracksNbr in myBarracks)
            {
                // Get the barracks
                Unit barracksUnit = GameManager.Instance.GetUnit(barracksNbr);

                // If this barracks still exists, is idle, we need archers, and have gold
                if (barracksUnit != null && barracksUnit.IsBuilt
                            && barracksUnit.CurrentAction == UnitAction.IDLE
                            && Gold >= Constants.COST[UnitType.ARCHER])
                {
                    Train(barracksUnit, UnitType.ARCHER);
                }
            }
        }

        /// <summary>
        /// Builds soliders
        /// </summary>
        public void BuildSoldier()
		{
            Debug.Log("<color=yellow>Built solider </color>AssetBundle");
            // For each barracks, determine if it should train a soldier or an archer
            foreach (int barracksNbr in myBarracks)
            {
                // Get the barracks
                Unit barracksUnit = GameManager.Instance.GetUnit(barracksNbr);

                // If this barracks still exists, is idle, we need soldiers, and have gold
                if (barracksUnit != null && barracksUnit.IsBuilt
                    && barracksUnit.CurrentAction == UnitAction.IDLE
                    && Gold >= Constants.COST[UnitType.SOLDIER])
                {
                    Train(barracksUnit, UnitType.SOLDIER);
                }
            }
        }

        #endregion

        #region Win

        /// <summary>
        /// Destroys the buildings
        /// </summary>
        public void DestroyBuildings()
		{
            Debug.Log("<color=yellow>Attack Everything </color>AssetBundle");
            if (myArchers.Count != 0)
            {
                AttackEnemy(myArchers);
            }

            if (mySoldiers.Count != 0)
            {
                AttackEnemy(mySoldiers);
            }
		}

		#endregion
	}
}

