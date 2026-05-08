using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityExplorerMcp.Models;

namespace UnityExplorerMcp.Core
{
    internal static class UiHierarchyTraverser
    {
        public static List<CanvasInfo> ListCanvases()
        {
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            var result = new List<CanvasInfo>();
            foreach (var c in canvases)
            {
                if (c.gameObject == null || c.gameObject.scene.name == null) continue;
                result.Add(new CanvasInfo
                {
                    name = c.gameObject.name,
                    path = GetPath(c.gameObject.transform),
                    sortingOrder = c.sortingOrder,
                    renderMode = c.renderMode.ToString(),
                    elementCount = CountChildren(c.transform)
                });
            }
            return result;
        }

        public static UiElementInfo GetHierarchy(string canvasName = null)
        {
            var root = new UiElementInfo
            {
                name = "Scene",
                path = "",
                children = new List<UiElementInfo>()
            };

            Canvas[] canvases;
            if (!string.IsNullOrEmpty(canvasName))
            {
                canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            }
            else
            {
                canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            }

            foreach (var canvas in canvases)
            {
                if (canvas.gameObject == null || canvas.gameObject.scene.name == null) continue;
                if (!string.IsNullOrEmpty(canvasName) &&
                    !canvas.gameObject.name.ToLower().Contains(canvasName.ToLower()))
                    continue;

                root.children.Add(TraverseTransform(canvas.transform, ""));
            }

            return root;
        }

        public static UiElementInfo TraverseTransform(Transform t, string parentPath)
        {
            var path = string.IsNullOrEmpty(parentPath)
                ? t.name
                : parentPath + "/" + t.name;

            var info = new UiElementInfo
            {
                name = t.name,
                path = path,
                active = t.gameObject.activeInHierarchy,
                tag = t.tag,
                layer = t.gameObject.layer,
                childCount = t.childCount,
                componentCount = t.gameObject.GetComponents<Component>().Length,
                rectTransform = GetRectTransformInfo(t),
                children = new List<UiElementInfo>()
            };

            for (int i = 0; i < t.childCount; i++)
            {
                info.children.Add(TraverseTransform(t.GetChild(i), path));
            }

            return info;
        }

        public static UiElementInfo GetElementByPath(string path)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                // Try to find by name search
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.scene.name == null) continue;
                    if (GetPath(obj.transform) == path || obj.name == path)
                    {
                        go = obj;
                        break;
                    }
                }
            }

            if (go == null) return null;
            return TraverseTransform(go.transform, "");
        }

        public static string GetPath(Transform t)
        {
            if (t == null) return "";
            var sb = new StringBuilder(t.name);
            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, "/");
                sb.Insert(0, t.name);
            }
            return sb.ToString();
        }

        public static List<UiElementInfo> SearchElements(string query, string componentType = null)
        {
            var results = new List<UiElementInfo>();
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var go in allObjects)
            {
                if (go.scene.name == null) continue;
                if (go.hideFlags.HasFlag(HideFlags.HideInHierarchy)) continue;

                bool nameMatch = string.IsNullOrEmpty(query) ||
                    go.name.ToLower().Contains(query.ToLower());
                if (!nameMatch) continue;

                if (!string.IsNullOrEmpty(componentType))
                {
                    if (!HasComponent(go, componentType)) continue;
                }

                var uiInfo = new UiElementInfo
                {
                    name = go.name,
                    path = GetPath(go.transform),
                    active = go.activeInHierarchy,
                    tag = go.tag,
                    layer = go.layer,
                    componentCount = go.GetComponents<Component>().Length,
                    rectTransform = GetRectTransformInfo(go.transform),
                    children = null
                };
                results.Add(uiInfo);
            }

            return results;
        }

        public static List<ComponentInfo> GetComponentDetails(string path)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.scene.name == null) continue;
                    if (GetPath(obj.transform) == path || obj.name == path)
                    {
                        go = obj;
                        break;
                    }
                }
            }

            if (go == null) return null;

            var components = go.GetComponents<Component>();
            var result = new List<ComponentInfo>();

            foreach (var comp in components)
            {
                if (comp == null) continue;
                var info = new ComponentInfo
                {
                    type = comp.GetType().Name,
                    enabled = true,
                    properties = new Dictionary<string, object>()
                };

                // Check if it's a Behaviour with enabled
                if (comp is Behaviour b) info.enabled = b.enabled;
                else if (comp is Renderer r) info.enabled = r.enabled;
                else if (comp is Collider col) info.enabled = col.enabled;

                // Get key properties based on type
                var compType = comp.GetType();

                if (comp is RectTransform rt)
                {
                    info.properties["anchoredPosition"] = rt.anchoredPosition.ToString();
                    info.properties["sizeDelta"] = rt.sizeDelta.ToString();
                    info.properties["anchorMin"] = rt.anchorMin.ToString();
                    info.properties["anchorMax"] = rt.anchorMax.ToString();
                    info.properties["pivot"] = rt.pivot.ToString();
                    info.properties["localPosition"] = rt.localPosition.ToString();
                    info.properties["position"] = rt.position.ToString();
                }
                else if (comp is Text txt)
                {
                    info.properties["text"] = txt.text;
                    info.properties["fontSize"] = txt.fontSize;
                    info.properties["color"] = txt.color.ToString();
                    info.properties["alignment"] = txt.alignment.ToString();
                    info.properties["font"] = txt.font?.name;
                }
                else if (comp is TextMeshProUGUI tmp)
                {
                    info.properties["text"] = tmp.text;
                    info.properties["fontSize"] = tmp.fontSize;
                    info.properties["color"] = tmp.color.ToString();
                    info.properties["alignment"] = tmp.alignment.ToString();
                    info.properties["font"] = tmp.font?.name;
                }
                else if (comp is Image img)
                {
                    info.properties["sprite"] = img.sprite?.name;
                    info.properties["color"] = img.color.ToString();
                    info.properties["raycastTarget"] = img.raycastTarget;
                }
                else if (comp is Button btn)
                {
                    info.properties["interactable"] = btn.interactable;
                    info.properties["transition"] = btn.transition.ToString();
                    info.properties["targetGraphic"] = btn.targetGraphic?.name;
                }
                else if (comp is Canvas canvas)
                {
                    info.properties["sortingOrder"] = canvas.sortingOrder;
                    info.properties["renderMode"] = canvas.renderMode.ToString();
                }
                else if (comp is CanvasGroup cg)
                {
                    info.properties["alpha"] = cg.alpha;
                    info.properties["interactable"] = cg.interactable;
                    info.properties["blocksRaycasts"] = cg.blocksRaycasts;
                }
                else if (comp is ScrollRect sr)
                {
                    info.properties["horizontal"] = sr.horizontal;
                    info.properties["vertical"] = sr.vertical;
                    info.properties["content"] = sr.content?.name;
                    info.properties["viewport"] = sr.viewport?.name;
                }
                else if (comp is InputField inf)
                {
                    info.properties["text"] = inf.text;
                    info.properties["interactable"] = inf.interactable;
                    info.properties["contentType"] = inf.contentType.ToString();
                }
                else if (comp is Toggle toggle)
                {
                    info.properties["isOn"] = toggle.isOn;
                    info.properties["interactable"] = toggle.interactable;
                }
                else if (comp is Slider slider)
                {
                    info.properties["value"] = slider.value;
                    info.properties["minValue"] = slider.minValue;
                    info.properties["maxValue"] = slider.maxValue;
                    info.properties["interactable"] = slider.interactable;
                }
                else if (comp is Dropdown dropdown)
                {
                    info.properties["value"] = dropdown.value;
                    info.properties["interactable"] = dropdown.interactable;
                    info.properties["options"] = dropdown.options.Count;
                }
                else if (comp is GridLayoutGroup glg)
                {
                    info.properties["cellSize"] = glg.cellSize.ToString();
                    info.properties["spacing"] = glg.spacing.ToString();
                    info.properties["constraint"] = glg.constraint.ToString();
                }
                else if (comp is VerticalLayoutGroup vlg)
                {
                    info.properties["spacing"] = vlg.spacing;
                    info.properties["padding"] = vlg.padding?.ToString();
                }
                else if (comp is HorizontalLayoutGroup hlg)
                {
                    info.properties["spacing"] = hlg.spacing;
                    info.properties["padding"] = hlg.padding?.ToString();
                }
                else if (comp is ContentSizeFitter csf)
                {
                    info.properties["horizontalFit"] = csf.horizontalFit.ToString();
                    info.properties["verticalFit"] = csf.verticalFit.ToString();
                }
                else
                {
                    // Generic: get limited public properties
                    int propCount = 0;
                    foreach (var prop in compType.GetProperties(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance))
                    {
                        if (prop.GetIndexParameters().Length > 0) continue;
                        if (!prop.CanRead) continue;
                        if (prop.Name == "name" || prop.Name == "hideFlags" ||
                            prop.Name == "gameObject" || prop.Name == "transform" ||
                            prop.Name == "tag")
                            continue;
                        if (propCount >= 20) { info.properties["..."] = "more properties omitted"; break; }
                        try
                        {
                            var val = prop.GetValue(comp);
                            if (val != null && (val is string || val is int ||
                                val is float || val is double || val is bool ||
                                val is long || val is Vector2 || val is Vector3 ||
                                val is Vector4 || val is Color || val is Enum))
                            {
                                info.properties[prop.Name] = val.ToString();
                            }
                        }
                        catch { }
                    }
                }

                result.Add(info);
            }

            return result;
        }

        private static bool HasComponent(GameObject go, string typeName)
        {
            var comps = go.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var name = c.GetType().Name;
                if (name == typeName) return true;
                // "Text" should also match TextMeshProUGUI / TMP_Text
                if (typeName == "Text" && (name == "TextMeshProUGUI" || name == "TMP_Text"))
                    return true;
                // "Image" should also match RawImage
                if (typeName == "Image" && name == "RawImage")
                    return true;
                // "Button" should also match Toggle-like behaviors if relevant
            }
            return false;
        }

        private static RectTransformInfo GetRectTransformInfo(Transform t)
        {
            var info = new RectTransformInfo
            {
                scaleX = 1f, scaleY = 1f,
                anchorMinX = 0f, anchorMinY = 0f,
                anchorMaxX = 1f, anchorMaxY = 1f,
                pivotX = 0.5f, pivotY = 0.5f
            };

            if (t is RectTransform rt)
            {
                info.posX = rt.position.x;
                info.posY = rt.position.y;
                info.width = rt.rect.width;
                info.height = rt.rect.height;
                info.anchorMinX = rt.anchorMin.x;
                info.anchorMinY = rt.anchorMin.y;
                info.anchorMaxX = rt.anchorMax.x;
                info.anchorMaxY = rt.anchorMax.y;
                info.pivotX = rt.pivot.x;
                info.pivotY = rt.pivot.y;
                info.localPosX = rt.localPosition.x;
                info.localPosY = rt.localPosition.y;
                info.scaleX = rt.localScale.x;
                info.scaleY = rt.localScale.y;
                info.rotation = rt.localEulerAngles.z;
            }
            else
            {
                info.posX = t.position.x;
                info.posY = t.position.y;
                info.width = 0;
                info.height = 0;
                info.localPosX = t.localPosition.x;
                info.localPosY = t.localPosition.y;
                info.scaleX = t.localScale.x;
                info.scaleY = t.localScale.y;
            }

            return info;
        }

        private static int CountChildren(Transform t)
        {
            int count = 1;
            for (int i = 0; i < t.childCount; i++)
                count += CountChildren(t.GetChild(i));
            return count;
        }

        public static string GetMouseElement()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return null;

            var data = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var graphicRaycaster = UnityEngine.Object.FindObjectOfType<GraphicRaycaster>();
            if (graphicRaycaster != null)
            {
                graphicRaycaster.Raycast(data, results);
            }
            else
            {
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                foreach (var c in canvases)
                {
                    var gr = c.GetComponent<GraphicRaycaster>();
                    if (gr != null)
                    {
                        data.position = Input.mousePosition;
                        gr.Raycast(data, results);
                    }
                }
            }

            if (results.Count > 0)
            {
                var top = results[0];
                return top.gameObject.name + "|" + GetPath(top.gameObject.transform);
            }

            return null;
        }
    }
}
