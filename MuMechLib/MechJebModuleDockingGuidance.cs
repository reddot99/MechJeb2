﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleDockingGuidance : DisplayModule
    {
        public MechJebModuleDockingGuidance(MechJebCore core) : base(core) { }

        MechJebModuleDockingAutopilot autopilot;
            

        protected override void FlightWindowGUI(int windowID)
        {
            if (!core.target.NormalTargetExists)
            {
                GUILayout.Label("Choose a target to dock with");
                base.FlightWindowGUI(windowID);
                return;
            }

            GUILayout.BeginVertical();

            if(autopilot == null) autopilot = core.GetComputerModule<MechJebModuleDockingAutopilot>();

            if (!(core.target.Target is ModuleDockingNode))
            {
                GUIStyle s = new GUIStyle(GUI.skin.label);
                s.normal.textColor = Color.yellow;
                GUILayout.Label("Warning: target is not a docking node. Target a docking node by right clicking it.", s);
            }

            bool onAxisNodeExists = false;
            foreach(ModuleDockingNode node in vessel.GetModules<ModuleDockingNode>()) 
            {
                if (Vector3d.Angle(node.GetTransform().forward, vessel.GetTransform().forward) < 2)
                {
                    onAxisNodeExists = true;
                    break;
                }
            }

            if (!onAxisNodeExists)
            {

                GUIStyle s = new GUIStyle(GUI.skin.label);
                s.normal.textColor = Color.yellow;
                GUILayout.Label("Warning: this vessel not controlled from a docking node. Right click the desired docking node on this vessel and select \"Control from here.\"", s);
            }

            autopilot.enabled = GUILayout.Toggle(autopilot.enabled, "Autopilot enabled");

            if (autopilot.enabled)
            {
                GUILayout.Label("Status: " + autopilot.status);
                Vector3d error = core.rcs.targetVelocity - vesselState.velocityVesselOrbit;
                double error_x = Vector3d.Dot(error, vessel.GetTransform().right);
                double error_y = Vector3d.Dot(error, vessel.GetTransform().forward);
                double error_z = Vector3d.Dot(error, vessel.GetTransform().up);
                GUILayout.Label("Error X: " + error_x.ToString("F2") + " m/s  [L/J]");
                GUILayout.Label("Error Y: " + error_y.ToString("F2") + " m/s  [I/K]");
                GUILayout.Label("Error Z: " + error_z.ToString("F2") + " m/s  [H/N]");
            }

            GUILayout.EndVertical();

            base.FlightWindowGUI(windowID);
        }

        public override GUILayoutOption[] FlightWindowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(300), GUILayout.Height(50) };
        }

        public override void OnModuleDisabled()
        {
            autopilot.enabled = false;
        }

        public override string GetName()
        {
            return "Docking Autopilot";
        }
    }
}