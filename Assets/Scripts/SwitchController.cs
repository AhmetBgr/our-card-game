using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class SwitchController : MonoBehaviour
{
    public Animator animator;
    private float longPressDur = 0.35f;
    private float _holdStartTime;
    private bool _isHolding = false;
    private Coroutine _longPressCoroutine;

    //public static event Action PointerLongPress;

    private void OnEnable()
    {
        GameManager.OnTurnSwitch += PlaySwitchAnim;
    }

    private void OnDisable()
    {
        GameManager.OnTurnSwitch -= PlaySwitchAnim;

    }

    private void PlaySwitchAnim(GameState gameState)
    {
        if(gameState == GameState.PlayerTurn)
        {
            animator.Play("PlayerSwitchAnim");
        }
        else if(gameState == GameState.OpponentTurn)
        {
            animator.Play("OpponentSwitchAnim");

        }
    }

    public void OnMouseDown() { 
        if(!GameManager.Instance.isPlayerTurn) return;

        // Start long press coroutine
        _isHolding = true;
        _longPressCoroutine = StartCoroutine(LongPressCheck());
    }
    public void OnMouseUp()
    {
        // Stop long press check if released early
        _isHolding = false;
        if (_longPressCoroutine != null)
        {
            StopCoroutine(_longPressCoroutine);
            _longPressCoroutine = null;
        }
    }
    private IEnumerator LongPressCheck()
    {
        yield return new WaitForSeconds(longPressDur);

        if (_isHolding)
        {
            GameManager.Instance.EndPlayerTurn();
            //PointerLongPress?.Invoke();
        }
    }

}
