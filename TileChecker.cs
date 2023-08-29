using LooterRivalry.Tiles;
using LooterRivalry.Tiles.Basic;
using UnityEngine;

public class TileChecker : MonoBehaviour
{
    private TileEntity tile; // ссылка на привязанный к тайлу объект TileEntity
    private GameObject isOccupedBy; // ссылка на объект, который на данный момент расположен на тайле  
    private bool isSelected = false;
    private static TileChecker clickedTileChecker;
    BoxCollider col;    

    private void Awake()
    {        
        Init();        
    }

    private void Start()
    {
        tile = MapEntity.instance.Tile(transform.position);
        tile.tileChecker = this;            
    }

    public void TileCheckerIsSelected(bool a)
    {
        isSelected = a;
        if(a)
        {
            print(tile + " " + isSelected);
        }

        else
        {
            
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        isOccupedBy = other.gameObject;
        tile.isOccuped = true;
    }

    private void OnTriggerExit(Collider other)
    {
        isOccupedBy = null;
        tile.isOccuped = false;
    }

    private void Init()
    {
        col = gameObject.AddComponent<BoxCollider>();
        col.size = transform.localScale;
        col.isTrigger = true;

        Rigidbody rig = gameObject.AddComponent<Rigidbody>();
        if (rig != null)
        {
            rig.useGravity = false;
        }
    }
    public GameObject GetIsOccupedBy()
    {
        return isOccupedBy;
    }

    public static TileChecker GetClickedTileChecker()
    {
        return clickedTileChecker;
    }

    public static void SetClickedTileCheckerToNull()
    {
        clickedTileChecker = null;
    }

    private void OnMouseDown()
    {
        if(Input.GetMouseButtonDown(0) && isSelected)
        {
            clickedTileChecker = this;
            PathDrawer.HideSelectedPointHighlight();
            print($"выбран тайл {GetClickedTileChecker().tile}");
        }
    }
}
