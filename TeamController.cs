using LootersRivalry.Tiles.Basic;
using System.Collections.Generic;
using UnityEngine;

public class TeamController : MonoBehaviour
{
    public List<UnitController> teamComposition;
    public static List<TeamController> teams;
    private bool isActive = false;

    private void Awake()
    {  
        if(teams == null)
        {
            teams = new List<TeamController>();
        }
        teams.Add(this);        
    }
    private void Start()
    {
        EndTurnToTeam();
    }
    private void ResetTeamRangeToInitial()
    {
        foreach (var a in teamComposition)
        {
            a.ResetRangeToInitial();
        }
    }

    private void SetTeamRangeToNull()
    {
        foreach (var unit in teamComposition)
        {
            unit.SetRangeToNull();
            if (unit.PathIsCreated())
            {
                unit.PathHide();
            }
            if (unit.AreaIsCreated())
            {
                unit.AreaHide();
            }
            UnitController.UnitResetActive();
        }
    }

    private void TeamSetActive(bool a)
    {
        isActive = a;
    }

    public bool GetTeamActivityStatus()
    {
        return isActive;
    }

    public void StartTurnToTeam()
    {
        TeamSetActive(true);
        if (teamComposition.Count > 0)
        {
            ResetTeamRangeToInitial();
        }
    }

    public void EndTurnToTeam()
    {
        TeamSetActive(false);
        UnitController.ClearTemporaryObjects();
        if (teamComposition.Count > 0)
        {
            SetTeamRangeToNull();
        }
    }
}