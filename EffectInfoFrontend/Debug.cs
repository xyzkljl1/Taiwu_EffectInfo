#define _DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

#if _DEBUG
namespace EffectInfo
{
    public class ModMono : MonoBehaviour
    {
        // Token: 0x06000011 RID: 17 RVA: 0x000025F3 File Offset: 0x000007F3
        private void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
        }

        // Token: 0x06000012 RID: 18 RVA: 0x0000261A File Offset: 0x0000081A
        private void OnDestroy()
        {
        }

        // Token: 0x06000013 RID: 19 RVA: 0x00002638 File Offset: 0x00000838
        public void Update()
        {
            List<RaycastResult> _raycastResults = new List<RaycastResult>();
            GameObject _currMouseOverObj = null;
            PointerEventData _pointerEventData = new PointerEventData(EventSystem.current);
            if (Input.GetKeyDown(KeyCode.A))
            {
                Vector2 screenMousePos = UIManager.Instance.UiCamera.ScreenToViewportPoint(Input.mousePosition);
                GameObject hitObj = null;
                if (screenMousePos.x >= 0f && screenMousePos.x <= 1f && screenMousePos.y >= 0f && screenMousePos.y <= 1f)
                {
                    _pointerEventData.position = Input.mousePosition;
                    EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);
                    if (_raycastResults.Count > 0)
                    {
                        hitObj = _raycastResults[0].gameObject;
                    }
                }
                bool mouseTipDisplayerActive = hitObj != null && hitObj.GetComponent<MouseTipDisplayer>() != null && hitObj.GetComponent<MouseTipDisplayer>().enabled;
                if (hitObj != _currMouseOverObj)
                {
                    UnityEngine.Debug.Log($"YYY:{hitObj.name} {hitObj.GetType().Name} {mouseTipDisplayerActive} ");
                    _currMouseOverObj = hitObj;
                }
            }
            //Input.GetKeyDown(this.main.key_switchAutoMove);
        }

        // Token: 0x04000019 RID: 25
        private int pressKeyCounterInc = 0;

        // Token: 0x0400001A RID: 26
        private int pressKeyCounterDec = 0;
    }
    public partial class EffectInfoFrontend
    {
        public static ModMono modMono;
        public static void DebugInitialize()
        {
            modMono = new GameObject("askldjaskldaskljd").AddComponent<ModMono>();
        }

    }
}
#endif