﻿using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Takes care of all things dialogue, whether they are coming from within a Timeline or just from the interaction with a character, or by any other mean.
/// Keeps track of choices in the dialogue (if any) and then gives back control to gameplay when appropriate.
/// </summary>
public class DialogueManager : MonoBehaviour
{
	//	[SerializeField] private ChoiceBox _choiceBox; // TODO: Demonstration purpose only. Remove or adjust later.
	[SerializeField]
	private List<ActorSO> _actorsList = default;
	[SerializeField] private InputReader _inputReader = default;
	private int _counterDialogue;
	private int _counterLine;
	private bool _reachedEndOfDialogue { get => _counterDialogue >= _currentDialogue._Lines.Count; }
	private bool _reachedEndOfLine { get => _counterLine >= _currentDialogue._Lines[_counterDialogue].TextList.Count; }

	[Header("Listening on channels")]
	[SerializeField] private DialogueDataChannelSO _startDialogue = default;
	[SerializeField] private DialogueChoiceChannelSO _makeDialogueChoiceEvent = default;

	[Header("BoradCasting on channels")]
	[SerializeField] private DialogueLineChannelSO _openUIDialogueEvent = default;
	[SerializeField] private DialogueChoicesChannelSO _showChoicesUIEvent = default;
	[SerializeField] private IntEventChannelSO _endDialogueWithTypeEvent = default;
	[SerializeField] private VoidEventChannelSO _continueWithStep = default;
	[SerializeField] private VoidEventChannelSO _playIncompleteDialogue = default;
	[SerializeField] private VoidEventChannelSO _makeWinningChoice = default;
	[SerializeField] private VoidEventChannelSO _makeLosingChoice = default;

	[Header("Gameplay Components")]
	[SerializeField]
	private GameStateSO _gameState = default;

	private DialogueDataSO _currentDialogue = default;

	private void Start()
	{
		_startDialogue.OnEventRaised += DisplayDialogueData;

	}

	/// <summary>
	/// Displays DialogueData in the UI, one by one.
	/// </summary>
	/// <param name="dialogueDataSO"></param>
	public void DisplayDialogueData(DialogueDataSO dialogueDataSO)
	{
		if (_gameState.CurrentGameState != GameState.Cutscene)
			_gameState.UpdateGameState(GameState.Dialogue);
		_counterDialogue = 0;
		_counterLine = 0;
		_inputReader.EnableDialogueInput();
		_inputReader.advanceDialogueEvent += OnAdvance;
		_currentDialogue = dialogueDataSO;


		if (_currentDialogue._Lines != null)
		{

			ActorSO currentActor = _actorsList.Find(o => o.ActorId == _currentDialogue._Lines[_counterDialogue].Actor); // we don't add a controle, because we need a null reference exeption if the actor is not in the list
			DisplayDialogueLine(_currentDialogue._Lines[_counterDialogue].TextList[_counterLine], currentActor);
		}

		else
		{
			Debug.LogError("Check Dialogue");
		}
	}
	//TODO : Check if there's no dependencies, and remove this function if none 
	/// <summary>
	/// Prepare DialogueManager when first time displaying DialogueData. 
	/// <param name="dialogueDataSO"></param>
	private void BeginDialogue(DialogueDataSO dialogueDataSO)
	{
		_counterDialogue = 0;
		_inputReader.EnableDialogueInput();
		_inputReader.advanceDialogueEvent += OnAdvance;
		_currentDialogue = dialogueDataSO;
	}

	/// <summary>
	/// Displays a line of dialogue in the UI, by requesting it to the <c>DialogueManager</c>.
	/// This function is also called by <c>DialogueBehaviour</c> from clips on Timeline during cutscenes.
	/// </summary>
	/// <param name="dialogueLine"></param>
	public void DisplayDialogueLine(LocalizedString dialogueLine, ActorSO actor)
	{
		_openUIDialogueEvent.RaiseEvent(dialogueLine, actor);
	}


	private void OnAdvance()
	{
		_counterLine++;
		if (!_reachedEndOfLine)
		{

			ActorSO currentActor = _actorsList.Find(o => o.ActorId == _currentDialogue._Lines[_counterDialogue].Actor); // we don't add a controle, because we need a null reference exeption if the actor is not in the list
			DisplayDialogueLine(_currentDialogue._Lines[_counterDialogue].TextList[_counterLine], currentActor);
			//do
		}
		else if (_currentDialogue._Lines[_counterDialogue].Choices != null && _currentDialogue._Lines[_counterDialogue].Choices.Count > 0)
		{
			if (_currentDialogue._Lines[_counterDialogue].Choices.Count > 0)
			{
				DisplayChoices(_currentDialogue._Lines[_counterDialogue].Choices);
			}
		}
		else
		{
			_counterDialogue++;
			if (!_reachedEndOfDialogue)
			{
				_counterLine = 0;

				ActorSO currentActor = _actorsList.Find(o => o.ActorId == _currentDialogue._Lines[_counterDialogue].Actor); // we don't add a controle, because we need a null reference exeption if the actor is not in the list
				DisplayDialogueLine(_currentDialogue._Lines[_counterDialogue].TextList[_counterLine], currentActor);
			}
			else
			{
				DialogueEndedAndCloseDialogueUI();
			}
		}
	}

	private void DisplayChoices(List<Choice> choices)
	{
		_inputReader.advanceDialogueEvent -= OnAdvance;

		_makeDialogueChoiceEvent.OnEventRaised += MakeDialogueChoice;
		_showChoicesUIEvent.RaiseEvent(choices);

	}

	private void MakeDialogueChoice(Choice choice)
	{
		_makeDialogueChoiceEvent.OnEventRaised -= MakeDialogueChoice;

		switch (choice.ActionType)
		{
			case ChoiceActionType.ContinueWithStep:
				if (_continueWithStep != null)
					_continueWithStep.RaiseEvent();
				if (choice.NextDialogue != null)
					DisplayDialogueData(choice.NextDialogue);
				break;
			case ChoiceActionType.WinningChoice:
				if (_makeWinningChoice != null)
					_makeWinningChoice.RaiseEvent();
				break;
			case ChoiceActionType.LosingChoice:
				if (_makeLosingChoice != null)
					_makeLosingChoice.RaiseEvent();

				break;
			case ChoiceActionType.DoNothing:
				if (choice.NextDialogue != null)
					DisplayDialogueData(choice.NextDialogue);
				else
					DialogueEndedAndCloseDialogueUI();
				break;
			case ChoiceActionType.IncompleteStep:
				if (_playIncompleteDialogue != null)
					_playIncompleteDialogue.RaiseEvent();
				if (choice.NextDialogue != null)
					DisplayDialogueData(choice.NextDialogue);
				break;

		}



	}

	public void CutsceneDialogueEnded()
	{
		if (_endDialogueWithTypeEvent != null)
			_endDialogueWithTypeEvent.RaiseEvent((int)DialogueType.DefaultDialogue);
	}
	void DialogueEndedAndCloseDialogueUI()
	{
		//raise the special event for end of dialogue if any 
		_currentDialogue.FinishDialogue();

		//raise end dialogue event 
		if (_endDialogueWithTypeEvent != null)
			_endDialogueWithTypeEvent.RaiseEvent((int)_currentDialogue.DialogueType);

		_inputReader.advanceDialogueEvent -= OnAdvance;
		_gameState.ResetToPreviousGameState();
		if (_gameState.CurrentGameState == GameState.Gameplay || _gameState.CurrentGameState == GameState.Combat)
			_inputReader.EnableGameplayInput();



	}
}

