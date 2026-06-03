using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public enum ShuttleKitType
    {
        Interior,
        Weapon,
        Shield,
        Power,
        Comfort,
        RideHailing
    }

    public enum RideOrderType
    {
        TransportPerson,
        TransportCargo
    }

    public enum RideDispatchMode
    {
        Mandatory,
        Optional
    }

    public enum RideOrderState
    {
        Pending,
        Accepted,
        PickedUp,
        Completed,
        Failed,
        Cancelled
    }

    public enum WeaponType
    {
        DualMachineGun,
        Autocannon,
        RocketPod,
        LaserCannon
    }

    public enum ShieldType
    {
        Simple,
        Energy,
        Reinforced
    }

    public enum PowerKitLevel
    {
        I,
        II,
        III
    }

    public enum ComfortKitLevel
    {
        I,
        II,
        III
    }
}
