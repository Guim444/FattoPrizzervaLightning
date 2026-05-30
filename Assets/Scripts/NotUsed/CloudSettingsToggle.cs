using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CloudSettingsToggle : MonoBehaviour
{
    public List<CloudElement> cloudElements = new List<CloudElement>();

    public void OnPress1(InputValue inputValue)
    {
        CloudToggle(1);
    }
    public void OnPress2(InputValue inputValue)
    {
        CloudToggle(2);
    }
    public void OnPress3(InputValue inputValue)
    {
        CloudToggle(3);
    }
    public void OnPress4(InputValue inputValue)
    {
        CloudToggle(4);
    }
    public void CloudActivateOnly(int value)
    {
        CloudElement element = cloudElements.Find(e => e.inputAssigned == value);

        foreach (CloudElement e in cloudElements)
        {
            e.cloud.SetActive(e == element);
        }
    }
    public void CloudToggle(int value)
    {
        CloudElement element = cloudElements.Find(e => e.inputAssigned == value);

        element.cloud.SetActive(!element.cloud.activeSelf);
    }
}


[Serializable]
public class CloudElement
{
    public GameObject cloud;
    public int inputAssigned;
}