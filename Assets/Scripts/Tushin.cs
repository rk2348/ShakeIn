using UnityEngine;
using TMPro;
using System.Collections;

public class ConnectingAnimation : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string baseMessage = "Connecting";
    [SerializeField] private float interval = 0.5f;

    private void Start()
    {
        if (loadingText == null)
        {
            loadingText = GetComponent<TMP_Text>();
        }

        StartCoroutine(AnimateText());
    }

    private IEnumerator AnimateText()
    {
        while (true)
        {
            loadingText.text = baseMessage;
            yield return new WaitForSeconds(interval);

            loadingText.text = baseMessage + ".";
            yield return new WaitForSeconds(interval);

            loadingText.text = baseMessage + "..";
            yield return new WaitForSeconds(interval);

            loadingText.text = baseMessage + "...";
            yield return new WaitForSeconds(interval);
        }
    }
}