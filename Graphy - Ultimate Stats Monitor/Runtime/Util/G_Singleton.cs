/* ---------------------------------------
 * Sourced from:    https://wiki.unity3d.com/index.php/Singleton
 * Modified by:     Martín Pane (martintayx@gmail.com) (@martinTayx)
 * Contributors:    https://github.com/Tayx94/graphy/graphs/contributors
 * Project:         Graphy - Ultimate Stats Monitor
 * Date:            07-Jul-17
 * Studio:          Tayx
 *
 * Git repo:        https://github.com/Tayx94/graphy
 *
 * This project is released under the MIT license.
 * Attribution is not required, but it is always welcomed!
 * -------------------------------------*/

using UnityEngine;

namespace Tayx.Graphy.Utils
{
    /// <summary>
    /// Be aware this will not prevent a non singleton constructor
    ///   such as `T myT = new T();`
    /// To prevent that, add `protected T () {}` to your singleton class.
    /// </summary>
    public class G_Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        #region Variables -> Private

        private static T _instance;

        private static object _lock = new object();

        #endregion

        #region Properties -> Public

        public static T Instance
        {
            get
            {
                lock( _lock )
                {
                    if( _instance == null )
                    {
                        Debug.Log
                        (
                            "[Singleton] An instance of " + typeof( T ) +
                            " is trying to be accessed, but it wasn't initialized first. " +
                            "Make sure to add an instance of " + typeof( T ) + " in the scene before " +
                            " trying to access it."
                        );
                    }

                    return _instance;
                }
            }
        }

        #endregion

        #region Methods -> Unity Callbacks

        void Awake()
        {
            // Project safety tweak:
            // In projects where many managers/controllers live on a shared root GameObject,
            // destroying the whole GameObject on duplicate-singleton detection can cascade and
            // wipe unrelated components. Destroy only this duplicate component instead.
            if( _instance != null && _instance != this )
            {
                Destroy( this );
                return;
            }

            _instance = GetComponent<T>();
        }

        void OnDestroy()
        {
            if( _instance == this )
            {
                _instance = null;
            }
        }

        #endregion
    }
}