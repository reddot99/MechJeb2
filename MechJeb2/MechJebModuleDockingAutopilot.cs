﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleDockingAutopilot : ComputerModule
    {
        public string status = "";

        public double approachSpeedMult = 0.2; // Approach speed will be approachSpeedMult * available thrust/mass on each axis.

        public double Kp = 0.2, Ki = 0, Kd = 0.00;

        public PIDController lateralPID;
        public PIDController zPID;

        [Persistent(pass = (int)(Pass.Type | Pass.Global))]
        [EditableInfoItem("Docking speed limit", InfoItem.Category.Thrust, rightLabel = "m/s")]
        public EditableDouble speedLimit = 0.5;
        [Persistent(pass = (int)Pass.Local)]
        public EditableDouble rol = new EditableDouble(0);
        [Persistent(pass = (int)Pass.Local)]
        public Boolean forceRol = false;

        public MechJebModuleDockingAutopilot(MechJebCore core)
            : base(core)
        {
            lateralPID = new PIDController(Kp, Ki, Kd);
            zPID = new PIDController(Kp, Ki, Kd);
        }

        public override void OnModuleEnabled()
        {
            core.attitude.attitudeRCScontrol = false;
            core.rcs.users.Add(this);
            core.attitude.users.Add(this);
            lateralPID = new PIDController(Kp, Ki, Kd);
            zPID = new PIDController(Kp, Ki, Kd);
        }

        public override void OnModuleDisabled()
        {
            core.attitude.attitudeRCScontrol = true;
            core.rcs.users.Remove(this);
            core.attitude.attitudeDeactivate();
        }

        private double FixSpeed(double s)
        {
            if (speedLimit != 0)
            {
                if (s >  speedLimit) s =  speedLimit;
                if (s < -speedLimit) s = -speedLimit;
            }
            return s;
        }

        public override void Drive(FlightCtrlState s)
        {
            if (!core.target.NormalTargetExists)
            {
                users.Clear();
                return;
            }

            if (forceRol)
                core.attitude.attitudeTo(Quaternion.LookRotation(Vector3d.back, Vector3d.up) * Quaternion.AngleAxis(-(float)rol, Vector3d.forward), AttitudeReference.TARGET_ORIENTATION, this);
            else
                core.attitude.attitudeTo(Vector3d.back, AttitudeReference.TARGET_ORIENTATION, this);

            Vector3d targetVel = core.target.Orbit.GetVel();

            Vector3d separation = core.target.RelativePosition;

            Vector3d zAxis = core.target.DockingAxis;
            double zSep = - Vector3d.Dot(separation, zAxis); //positive if we are in front of the target, negative if behind
            
            Vector3d lateralSep = Vector3d.Exclude(zAxis, separation);

            double zApproachSpeed = FixSpeed( Math.Sqrt( Math.Abs(zSep) * vesselState.rcsThrustAvailable.GetMagnitude(-zAxis) * approachSpeedMult / vesselState.mass ));
            
            double latApproachSpeed = FixSpeed( Math.Sqrt(lateralSep.magnitude * vesselState.rcsThrustAvailable.GetMagnitude(-lateralSep) * approachSpeedMult / vesselState.mass));

            //print("zSep=" + zSep.ToString("F2") + " lSep=" + lateralSep.magnitude.ToString("F2") + " zSpd=" + zApproachSpeed.ToString("F2") +" lSpd=" + latApproachSpeed.ToString("F2") );


            if (zSep < 0)  //we're behind the target
            {
                if (lateralSep.magnitude < 10) //and we'll hit the target if we back up
                {
                    core.rcs.SetTargetWorldVelocity(targetVel + zApproachSpeed * lateralSep.normalized); //move away from the docking axis
                    status = "Moving away from docking axis at " + zApproachSpeed.ToString("F2") + " m/s to avoid hitting target on backing up";
                }
                else
                {
                    double backUpSpeed = FixSpeed(-zApproachSpeed * Math.Max(1, -zSep / 50));
                    core.rcs.SetTargetWorldVelocity(targetVel + backUpSpeed * zAxis); //back up
                    status = "Backing up at " + backUpSpeed.ToString("F2") + " m/s to get on the correct side of the target to dock.";
                }
                lateralPID.Reset();
            }
            else //we're in front of the target
            {
                //move laterally toward the docking axis
                lateralPID.max = latApproachSpeed;
                lateralPID.min = -lateralPID.max;
                
                //Vector3d lateralVelocityNeeded = -lateralSep.normalized * lateralPID.Compute(latApproachSpeed);
                Vector3d lateralVelocityNeeded = -lateralSep.normalized * latApproachSpeed;
                
                //if (lateralVelocityNeeded.magnitude > latApproachSpeed) lateralVelocityNeeded *= (latApproachSpeed / lateralVelocityNeeded.magnitude);

                //print("lateralVelocityNeeded=" + lateralVelocityNeeded.magnitude.ToString("F2"));

                //double zVelocityNeeded = zApproachSpeed;
                zPID.max = zApproachSpeed;
                zPID.min = -zPID.max;
                //double zVelocityNeeded = zPID.Compute(zApproachSpeed);
                double zVelocityNeeded = zApproachSpeed;

                if (lateralSep.magnitude > 0.2 && lateralSep.magnitude * 10 > zSep)
                {
                    //we're very far off the docking axis
                    if (zSep < lateralSep.magnitude)
                    {
                        //we're far off the docking axis, but our z separation is small. Back up to increase the z separation
                        zVelocityNeeded *= -1;
                        status = "Backing at " + zVelocityNeeded.ToString("F2") + " m/s up and moving toward docking axis.";
                    }
                    else
                    {
                        //we're not extremely close in z, so just stay at this z distance while we fix the lateral separation
                        //zVelocityNeeded = 0;
                        //print("zVelocityNeeded=" + zVelocityNeeded.ToString("F2") + " lateralVelocityNeeded=" + lateralVelocityNeeded.magnitude.ToString("F2"));
                        // we're not extremely close in z, move forward but slow engough so we are on docking axis before we are near the dock
                        zVelocityNeeded = Math.Min(zVelocityNeeded, (zSep * lateralVelocityNeeded.magnitude) / lateralSep.magnitude);
                        //print("zVelocityNeeded=" + zVelocityNeeded.ToString("F2"));
                        status = "Moving toward the docking axis at " + lateralVelocityNeeded.magnitude.ToString("F2") + " m/s.";
                    }
                }
                else
                {
                    if (zSep > 0.4)
                    {
                        //we're not extremely far off the docking axis. Approach the along z with a speed determined by our z separation
                        //but limited by how far we are off the axis
                        status = "Moving forward to dock at " + zVelocityNeeded.ToString("F2") + " m/s.";
                    }
                    else
                    {
                        // close enough, turn it off and let the magnetic dock work
                        users.Clear();
                        return;
                    }
                }

                Vector3d adjustment = lateralVelocityNeeded + zVelocityNeeded * zAxis.normalized;
                double magnitude = adjustment.magnitude;
                if (magnitude > 0) adjustment *= FixSpeed(magnitude) / magnitude;
                core.rcs.SetTargetWorldVelocity(targetVel + adjustment);
            }
        }
    }
}
