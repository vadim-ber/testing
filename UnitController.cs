using LootersRivalry.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UIDialogs;
using UnityEngine;

namespace LootersRivalry.Tiles.Basic
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
        private bool isCoroutineRunning = false;
        private Animator animator;
        private List<UnitController> targets;
        private static GameObject temporaryObjects;
        private static UnitController isActive = null;
        public Transform RotationNode;        
        public TeamController team;

        MapEntity Map;
        static AreaOutline Area;
        static PathDrawer Path;
        Coroutine MovingCoroutine;

        void Update()
        {
            if (this.Equals(isActive) && team.GetTeamActivityStatus() && isCoroutineRunning == false)
            {
                if (MyInput.GetOnWorldUp(Map.Settings.Plane()))
                {
                    HandleWorldClick();
                }
                PathUpdate();
                UnitHighlightUpdate();
            }
            ShowDialogWindowWithRay();            
        }

        private void FixedUpdate()
        {
            animator.speed = moveSpeed * Time.fixedDeltaTime * 10;
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
            animator = GetComponent<Animator>();

            if (!temporaryObjects)
            {
                temporaryObjects = GameObject.Find("TemporaryObjects");
            }

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
        }

        public static void ClearTemporaryObjects()
        {
            var list1 = temporaryObjects.GetComponentsInChildren<AreaOutline>();            
            foreach (var p in list1)
            {
                Destroy(p.gameObject);
            }
            var list2 = temporaryObjects.GetComponentsInChildren<PathDrawer>();
            foreach (var p in list2)
            {
                Destroy(p.gameObject);
            }
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

        IEnumerator Moving(List<TileEntity> path, Action onCompleted, bool needRotation)
        {
           var nextIndex = 1;
           transform.position = Map.Settings.Projection(transform.position);
           animator.SetFloat("Speed", moveSpeed);
           animator.SetFloat("MotionSpeed", 1);
           while (nextIndex < path.Count)
           {
                var targetPoint = Map.WorldPosition(path[nextIndex]);
                var stepDir = (targetPoint - transform.position) * moveSpeed;
                if (needRotation)
                {
                    yield return StartCoroutine(RotateCoroutine(this, stepDir));
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
            animator.SetFloat("Speed", 0);
            animator.SetFloat("MotionSpeed", 0);
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
            if(Area)
            {
                Area.Show(Map.WalkableBorder(transform.position, moveRange), Map);
                Area.transform.SetParent(temporaryObjects.transform);
            }
            else
            {
                Area = Spawner.Spawn(AreaPrefab, Vector3.zero, Quaternion.identity);
                Area.transform.SetParent(temporaryObjects.transform);
            }
        }

        public void AreaHide()
        {
            if(Area)
            {
                Area.Hide();
            }            
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
                if (tile != null && tile.Vacant && Area)
                {
                    var path = Map.PathPoints(transform.position, Map.WorldPosition(tile.Position), moveRange);
                    Path.Show(path, Map);
                    Path.ActiveState();
                    Area.ActiveState();
                }
                else
                {
                    Path.InactiveState();
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

        public void UnitSetActive()
        {
            if (isActive)
            {
                if(Path)
                {
                    isActive.PathHide();
                    Path.HideSelectedUnitHighlight();
                    Path = null;
                }
                isActive.AreaHide();
                Area = null;
                PathDrawer.HideSelectedPointHighlight();
                StopAllCoroutines();
                isActive = null;
            }            
            isActive = this;
            PathCreate();
            Area = Spawner.Spawn(AreaPrefab, Vector3.zero, Quaternion.identity);
            AreaShow();
            targets = CreateListOfUnitsInAttackRadius();
            GameLog.Instance.Print($"{name} активен");            
        } 

        public static void UnitResetActive()
        {
            isActive = null;
        }

        public static UnitController GetUnitActivityStatus()
        {
            return isActive;
        }

        void OnMouseDown()
        {   
            if(team.GetTeamActivityStatus() && !isCoroutineRunning)
            {
                UnitSetActive();
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


        void ShowDialogWindowWithRay()
        {
            if (Input.GetMouseButtonDown(1))
            {
                PathHide();
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    UnitController unit1 = hit.collider.GetComponent<UnitController>();
                    if (unit1 && unit1.Equals(this) && !team.GetTeamActivityStatus() && unit1.isCoroutineRunning == false)
                    {
                        targets = CreateListOfUnitsInAttackRadius();
                        UnitController unit2 = targets.Find(n => n.Equals(isActive) && n.targets.Contains(this));
                        if (unit2)
                        {
                            AreaHide();
                            PathHide();
                            unit1.isCoroutineRunning = true;
                            unit2.isCoroutineRunning = true;
                            DialogUI.Instance.SetUnitControllers(unit1, unit2);
                            DialogUI.Instance.SetTitle(gameObject.name).OnClose((unit1, unit2) =>
                            {
                                StartCoroutine(OnDialogClose(unit1, unit2));
                            }).SetPosition(gameObject.transform.position).Show();
                        }
                    }
                }
            }
        }

        private IEnumerator OnDialogClose(UnitController unit1, UnitController unit2)
        {
            if (unit1 != null && unit2 != null)
            {
                unit2.AreaHide();
                unit2.PathHide();
                yield return StartCoroutine(RotateUnitToUnit(unit1, unit2));
                yield return StartCoroutine(PushUnitByUnit(unit1, unit2));
                unit2.isCoroutineRunning = false;
            }            
        }

       private IEnumerator RotateUnitToUnit(UnitController unit1, UnitController unit2)
        {            
            var targetDir1 = unit1.transform.position - unit2.transform.position;
            var targetDir2 = unit2.transform.position - unit1.transform.position;
            yield return StartCoroutine(RotateCoroutine(unit2, targetDir1));
            yield return StartCoroutine(RotateCoroutine(unit1, targetDir2));
        }

        private IEnumerator RotateCoroutine(UnitController unit, Vector3 targetDirection)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            var angle = Vector3.Angle(transform.forward, targetDirection);
            var cross = Vector3.Cross(transform.forward, targetDirection);
            if (cross.y < 0)
            {
                angle = -angle;
            }
            animator.SetFloat("Angle", angle);           

            while (unit.transform.rotation != targetRotation)
            {
                unit.transform.rotation = Quaternion.RotateTowards(unit.transform.rotation, targetRotation, rotateSpeed * Time.deltaTime * 500);
                yield return null;         
            }
            animator.SetTrigger("StopRotation");
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
                    if ((pushTile.Position - aTile.Position) == offset)
                    {
                        if (pushTile != bTile && pushTile.Vacant)
                        {
                            pushTiles.Add(pushTile);
                        }
                        areapositions2 = Map.AreaPositions(pushTile.Position, 1f);
                        foreach (var tile1 in areapositions1)
                        {
                            foreach (var tile2 in areapositions2)
                            {
                                if (tile1 == tile2 && Map.Tile(tile2).Vacant && Map.Tile(tile2) != bTile && tile2 != vector)
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
            yield return new WaitUntil(() => TileChecker.GetClickedTileChecker() != null);
            if(possibleTiles.Contains(Map.Tile(TileChecker.GetClickedTileChecker().transform.position)))
            {
                yield return StartCoroutine(PushAndMove(unit1, unit2,true));
            }            
        }

        private IEnumerator PushAndMove(UnitController unit1, UnitController unit2, bool needToFollow)
        {            
            if (!unit2.team.GetTeamActivityStatus())
            {
                yield break;
            }
            var formerPoint = unit1.transform.position;
            var path1 = Map.PathTiles(formerPoint, TileChecker.GetClickedTileChecker().transform.position, 1);
            int dice = Dice.DiceD6Roll();
            string animName = null;
            if(dice < 4)
            {
                animName = "Strike_to_head2";
            }
            else { animName = "Strike_to_head1"; }
            print(animName);
            yield return AnimatorExt.PlayAndWait(unit2.animator, animName, 0);            
            StartCoroutine(Moving(path1, () => {}, false));
            if (needToFollow)
            {
                List<TileEntity> path2 = new();
                path2.Add(Map.Tile(unit2.transform.position));
                path2.Add(Map.Tile(formerPoint));
                yield return StartCoroutine(unit2.Moving(path2, () => { }, true));
            }
            yield return StartCoroutine(RotateUnitToUnit(this, unit2));
            unit1.isCoroutineRunning = false;
            unit2.isCoroutineRunning = false;
        }

        private void OnFootstep()
        {
            //топот маленьких ножек
        }
    }
}
