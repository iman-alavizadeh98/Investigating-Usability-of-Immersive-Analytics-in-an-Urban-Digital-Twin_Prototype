using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanAnalytics.XR
{
    /// <summary>The VR functions the tutorial unlocks one at a time.</summary>
    public enum XRFeature
    {
        /// <summary>Trigger on the city selects; B clears.</summary>
        Select,

        /// <summary>Toolbar "&lt; View / View &gt;" and the wrist Views tab.</summary>
        ChangeView,

        /// <summary>Left grip / stick: move, turn and recall the table.</summary>
        MoveTable,

        /// <summary>Right stick up/down, toolbar Smaller / Bigger / Turn / Bring here.</summary>
        ResizeTable,

        /// <summary>Toolbar Copy tool and Remove copies.</summary>
        CopyTool,

        /// <summary>Grip on a copy or on a panel's Grab bar.</summary>
        Grab,

        /// <summary>Toolbar Compare tool.</summary>
        CompareTool,

        /// <summary>Look at the left wrist; X.</summary>
        WristMenu,

        /// <summary>Toolbar Clear table and Buildings on/off.</summary>
        ClearTable
    }


    /// <summary>
    /// Which VR functions may be used right now. Everything is allowed
    /// unless the tutorial (XRTutorial) has locked the rest: then only
    /// the functions it has taught so far work, so new users learn the
    /// controls one at a time. Controls check <see cref="Allows"/>;
    /// buttons grey out on <see cref="Changed"/>.
    /// </summary>
    public static class XRFeatureLock
    {
        private static HashSet<XRFeature> allowed;


        /// <summary>Raised when the lock starts, changes or ends.</summary>
        public static event Action Changed;


        /// <summary>True while the tutorial restricts the functions.</summary>
        public static bool IsLocked =>
            allowed != null;


        public static bool Allows(
            XRFeature feature
        )
        {
            return allowed == null ||
                   allowed.Contains(feature);
        }


        /// <summary>Only these functions work until <see cref="Unlock"/>.</summary>
        public static void LockAllExcept(
            IEnumerable<XRFeature> features
        )
        {
            allowed =
                new HashSet<XRFeature>(features);

            Changed?.Invoke();
        }


        public static void Unlock()
        {
            if (allowed == null)
            {
                return;
            }

            allowed =
                null;

            Changed?.Invoke();
        }


        // Static state survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            allowed =
                null;

            Changed =
                null;
        }
    }
}
