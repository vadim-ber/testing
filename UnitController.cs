using LooterRivalry.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UIDialogs;
using UnityEngine;

namespace LooterRivalry.Tiles.Basic
{
    public class UnitController : MonoBehaviour
    {
        [Range(0, 10)] public int moveRange = 4;
        [Range(0, 10)] public int attackRange = 1;
        [Range(0, 10)] public float moveSpeed = 5;
        [Range(0, 10)] public float rotateSpeed = 1.0f;
        [SerializeField] AreaOutline AreaPrefab;
        [SerializeField] PathDrawer PathPrefab;
        private int initialMoveRange; 
        private bool isActive = false;
        private bool isCoroutineRunning = false;
        private List<UnitController> targets;
        private GameObject temporaryObjects;        
        public Transform RotationNode;        
        public TeamController team;        

        MapEntity Map;
        AreaOutline Area;
        PathDrawer Path;
        Coroutine MovingCoroutine;

        void Update()
        {
            if (GetUnitActivityStatus() && team.GetTeamActivityStatus())
            {
                if (MyInput.GetOnWorldUp(Map.Settings.Plane()))
                {
                    HandleWorldClick();
                }
                PathUpdate();
                UnitHighlightUpdate();
            }            
        }

        public void Init(MapEntity map)
        {
            initialMoveRange = moveRange;

            if (AreaPrefab == null)
            {
                AreaPrefab = Resources.Load<AreaOutline>("Prefabs/Area/AreaOutline");
            }

            if (PathPrefab == null)
            {
                PathPrefab = Resources.Load<PathDrawer>("Prefabs/Path/PathDrawer");
            }            

            Map = map;
            Area = Spawner.Spawn(AreaPrefab, Vector3.zero, Quaternion.identity);
            temporaryObjects = GameObject.Find("TemporaryObjects");
            Area.transform.SetParent(temporaryObjects.transform);

            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();           
            col.radius = 0.6f;
            col.height = 2.1f;

            Rigidbody rig = gameObject.AddComponent<Rigidbody>();
            if (rig != null)
            {
                rig.useGravity = false;
                rig.isKinematic = true;
            }

            if (transform.parent != null && transform.parent.GetComponent<TeamController>())
            {
                team = transform.parent.GetComponent<TeamController>();
                team.teamComposition.Add(this);
            }
            AreaHide();
            PathCreate();
        }

        void HandleWorldClick()
        {
            var clickPos = MyInput.GroundPosition(Map.Settings.Plane());
            var tile = Map.Tile(clickPos);            
            if (tile != null && tile.Vacant)
            {                
                AreaHide();
                Path.IsEnabled = false;
                PathHide();
                var path = Map.PathTiles(transform.position, clickPos, moveRange);                
                Move(path, () =>
                {
                    Path.IsEnabled = true;
                    AreaShow();
                    targets = CreateListOfUnitsInAttackRadius();
                });               
            }
        }

        public void Move(List<TileEntity> path, Action onCompleted)
        {
            if(moveRange > 0)
            {
                if (path != null)
                {
                    if (MovingCoroutine != null)
                    {
                        StopCoroutine(MovingCoroutine);
                    }
                    MovingCoroutine = StartCoroutine(Moving(path, onCompleted, true));
                }
                else
                {
                    onCompleted.SafeInvoke();
                }
            }
        }

        IEnumerator Moving(List<TileEntity> path, Action onCompleted, bool needRot)
        {
           var nextIndex = 1;
           transform.position = Map.Settings.Projection(transform.position);
           while (nextIndex < path.Count)
           {
             var targetPoint = Map.WorldPosition(path[nextIndex]);
             var stepDir = (targetPoint - transform.position) * moveSpeed;
             if (needRot && Map.RotationType == RotationType.LookAt)
             {
               StartCoroutine(RotateCoroutine(this, stepDir));
             }
             else if (needRot && Map.RotationType == RotationType.Flip)
             {
               RotationNode.rotation = Map.Settings.Flip(stepDir);
             }
             var reached = stepDir.sqrMagnitude < 0.01f;
             while (!reached)
             {
               transform.position += stepDir * Time.deltaTime;
               reached = Vector3.Dot(stepDir, (targetPoint - transform.position)) < 0f;
               yield return null;
             }
             transform.position = targetPoint;
             nextIndex++;
             moveRange = Mathf.Max(0, moveRange - 1);
           }
                onCompleted.SafeInvoke();
        }
        public void ResetRangeToInitial()
        {
            moveRange = initialMoveRange;
        }

        public void SetRangeToNull()
        {
            moveRange = 0;
        }

        public void AreaShow()
        {
            if(team.GetTeamActivityStatus())
            {
                AreaHide();
                Area.Show(Map.WalkableBorder(transform.position, moveRange), Map);
            }            
        }

        public void AreaHide()
        {
            Area.Hide();
        }

        public bool AreaIsCreated()
        {
            return Area != null;
        }

        void PathCreate()
        {
            if (!Path)
            {
                Path = Spawner.Spawn(PathPrefab, Vector3.zero, Quaternion.identity);
                Path.Show(new List<Vector3>() { }, Map);
                Path.InactiveState();
                Path.IsEnabled = true;
                Path.transform.SetParent(temporaryObjects.transform);
            }
        }

        public void PathHide()
        {
            if (Path)
            {
                Path.Hide();
            }
        }

        void PathUpdate()
        {
            if (Path && Path.IsEnabled)
            {
                var tile = Map.Tile(MyInput.GroundPosition(Map.Settings.Plane()));
                if (tile != null && tile.Vacant)
                {
                    var path = Map.PathPoints(transform.position, Map.WorldPosition(tile.Position), moveRange);
                    Path.Show(path, Map);
                    Path.ActiveState();
                    Area.ActiveState();
                }
                else
                {
                    Path.InactiveState();
                    Area.InactiveState();
                }
            }
        }
        void UnitHighlightUpdate()
        {
           Path.ShowSelectedUnittHighlight(this, Map);            
        }

        public bool PathIsCreated()
        {
            return Path != null;
        }

        public void UnitSetActive(bool a)
        {
            
            if(a)
            {
                foreach (var unit in FirstStart.arrayAllUnits)
                {
                    if(!isCoroutineRunning)
                    {
                        unit.UnitSetActive(false);
                    }                    
                }
                isActive = a;
                AreaShow();
                targets = CreateListOfUnitsInAttackRadius();
                GameLog.Instance.Print($"{name} активен");
            }
            else
            {
                isActive = a;
                AreaHide();
                PathHide();
                Path.HideSelectedUnitHighlight();
                PathDrawer.HideSelectedPointHighlight();
            }
        } 

        public bool GetUnitActivityStatus()
        {
            return isActive;
        }

        void OnMouseDown()
        {              
            if(team.GetTeamActivityStatus() && !isCoroutineRunning)
            {
                UnitSetActive(true);
            }            
        }

        public List<UnitController> CreateListOfUnitsInAttackRadius()
        {
            List<UnitController> targets = new();
            Collider[] hits = Physics.OverlapCapsule(transform.position, transform.position + transform.up * 2f, attackRange *2);
            foreach(var h in hits)
            {
                if(h.gameObject.GetComponent<UnitController>() && h.gameObject != gameObject)
                {
                    targets.Add(h.gameObject.GetComponent<UnitController>());
                }
            }            
            return targets;
        }

        IEnumerator OnMouseOver()
        {
            if (Input.GetMouseButtonDown(1) && !team.GetTeamActivityStatus())
            {
                targets = CreateListOfUnitsInAttackRadius();
                foreach(var n in targets)
                {                    
                    n.targets = n.CreateListOfUnitsInAttackRadius();
                    if (!n.isCoroutineRunning && n.team.GetTeamActivityStatus() && n.targets.Contains(this) && n.isActive)
                    {
                        n.UnitSetActive(false);
                        n.AreaHide();
                        n.PathHide();
                        isCoroutineRunning = true;
                        n.isCoroutineRunning = true;                        
                        yield return StartCoroutine(RotateUnitToUnit(this, n));                        
                        DialogUI.Instance.SetUnitControllers(this, n);
                        DialogUI.Instance.SetTitle(gameObject.name).OnClose((unit1, unit2) =>
                        {
                            StartCoroutine(OnDialogClose(this, n));
                        }).SetPosition(gameObject.transform.position).Show();
                        break;
                    }
                }
            }
        }

        private IEnumerator OnDialogClose(UnitController unit1, UnitController unit2)
        {
            yield return StartCoroutine (PushUnitByUnit(unit1, unit2));            
            unit2.isCoroutineRunning = false;
        }

       private IEnumerator RotateUnitToUnit(UnitController unit1, UnitController unit2)
        {
            var targetDir1 = unit1.transform.position - unit2.transform.position;
            var targetDir2 = unit2.transform.position - unit1.transform.position;
            yield return StartCoroutine(RotateCoroutine(unit2, targetDir1));
            yield return StartCoroutine(RotateCoroutine(unit1, targetDir2));
            //проигрывание анимации
        }

        private IEnumerator RotateCoroutine(UnitController unit, Vector3 targetDirection)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            while (unit.transform.rotation != targetRotation)
            {
                unit.transform.rotation = Quaternion.RotateTowards(unit.transform.rotation, targetRotation, rotateSpeed * Time.deltaTime * 500);
                yield return null;
            }
            unit.isCoroutineRunning = false;
        }

        private List<TileEntity> SearchPuchableTiles(UnitController unit1, UnitController unit2)
        {
            {
                unit2.SetRangeToNull();

                var bTile = Map.Tile(unit2.transform.position);
                var aTile = Map.Tile(unit1.transform.position);
                var pushTiles = new List<TileEntity>();

                Vector3Int offset = aTile.Position - bTile.Position;

                List<Vector3> areapositions1 = Map.AreaPositions(aTile.Position, 1f);
                List<Vector3> areapositions2;

                foreach (var vector in areapositions1)
                {
                    var pushTile = Map.Tile(vector);
                    if((pushTile.Position - aTile.Position) == offset)
                    {
                        if (pushTile != bTile && pushTile.Vacant)
                        {                            
                            pushTiles.Add(pushTile);
                        }
                        areapositions2 = Map.AreaPositions(pushTile.Position, 1f);
                        foreach(var tile1 in areapositions1)
                        {
                            foreach(var tile2 in areapositions2)
                            {
                                if(tile1 == tile2 && Map.Tile(tile2).Vacant && Map.Tile(tile2)!= bTile && tile2 != vector)
                                {
                                    pushTiles.Add(Map.Tile(tile2));
                                }
                            }
                        }
                    }                                  
                }
                GameLog.Instance.Print($"{unit2.name} толкает {unit1.name}");
                return pushTiles;                
            }            
        }

        public IEnumerator PushUnitByUnit(UnitController unit1, UnitController unit2)
        {
            TileChecker.SetClickedTileCheckerToNull();
            var possibleTiles = SearchPuchableTiles(unit1, unit2);           
            foreach (var tile in possibleTiles)
            {
                Path.ShowSelectedPointHighlight(tile.tileChecker.transform.position, Map);                
            }            
            yield return StartCoroutine(PushAndMove(unit1, unit2, true));
        }

        private IEnumerator PushAndMove(UnitController unit1, UnitController unit2, bool needToFollow)
        {
            yield return new WaitUntil(() => TileChecker.GetClickedTileChecker() != null);
            if(!unit2.team.GetTeamActivityStatus())
            {
                yield break;
            }
            var path1 = Map.PathTiles(unit1.transform.position, TileChecker.GetClickedTileChecker().transform.position, 1);
            if(needToFollow)
            {
                List<TileEntity> path2 = new();
                path2.Add(Map.Tile(unit2.transform.position));
                path2.Add(Map.Tile(unit1.transform.position));
                StartCoroutine(unit2.Moving(path2, () => { }, true));
            }
            StartCoroutine(Moving(path1, () => { print($"{unit1.name} вытолкнут на {TileChecker.GetClickedTileChecker()}") ; },false));
            unit1.isCoroutineRunning = false;
            unit2.isCoroutineRunning = false;
        }
    }
}
