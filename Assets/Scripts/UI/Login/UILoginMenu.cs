/*
* Copyright (c) 2021 PlayEveryWare
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.UI;
using Epic.OnlineServices.Ecom;
using Epic.OnlineServices.Logging;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using PlayEveryWare.EpicOnlineServices;

namespace PlayEveryWare.EpicOnlineServices.Samples
{
    using Epic.OnlineServices.Connect;
    using System.Linq;
    using LoginCallbackInfo = Epic.OnlineServices.Auth.LoginCallbackInfo;

    public class UILoginMenu : MonoBehaviour
    {
        [Header("Authentication UI")]
        public Text DemoTitle;
        public Dropdown SceneSwitcherDropDown;
        public Dropdown loginTypeDropdown;

        public RectTransform idContainer;
        public Text idText;
        public UIConsoleInputField idInputField;

        public Text tokenText;
        public UIConsoleInputField tokenInputField;
        public UITooltip tokenTooltip;

        public RectTransform connectTypeContainer;
        public Dropdown connectTypeDropdown;

        public Text loginButtonText;
        private string _OriginalloginButtonText;
        public Button loginButton;
        private Coroutine PreventLogIn = null;
        public Button logoutButton;
        public Button removePersistentTokenButton;

        public UnityEvent OnLogin;
        public UnityEvent OnLogout;

        [Header("Controller")]
        public GameObject UIFirstSelected;
        public GameObject UIFindSelectable;

        // NOTE:  Use to indicate Connect login instead of Auth (note that
        //        "Connect" is not a valid value within the enum
        //        "LoginCredentialType."
        private const LoginCredentialType ConnectType = (LoginCredentialType)(-1);

        private LoginCredentialType loginType = LoginCredentialType.Developer;
        
        // NOTE: Use to indicate an invalid external credential type
        private const ExternalCredentialType invalidCredentialType = (ExternalCredentialType)(-1);
        private ExternalCredentialType externalCredentialType = invalidCredentialType;

        Apple.EOSSignInWithAppleManager signInWithAppleManager = null;

        // Retain Id/Token inputs across scenes
        public static string IdGlobalCache = string.Empty;
        public static string TokenGlobalCache = string.Empty;

        private void Awake()
        {
            idInputField.InputField.onEndEdit.AddListener(CacheIdInputField);
            tokenInputField.InputField.onEndEdit.AddListener(CacheTokenField);

            SetDefaultLoginType(ref loginType);

            // TODO: This will fail on anything that is mac, windows, or linux, or is an editor version of any of the above
#if UNITY_EDITOR || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            idInputField.InputField.text = "localhost:7777"; //default on pc
#endif

            CheckInputSystem();
        }

        /// <summary>
        /// Checks the input system, and logs an error if there is a problem with the configuration.
        /// </summary>
        private static void CheckInputSystem()
        {
#if !ENABLE_INPUT_SYSTEM && (UNITY_XBOXONE || UNITY_GAMECORE_XBOXONE || UNITY_GAMECORE_SCARLETT || UNITY_PS4 || UNITY_PS5 || UNITY_SWITCH)
            Debug.LogError("Input currently handled by Input Manager. Input System Package is required for controller support on consoles.");
#endif
        }

        private static void SetDefaultLoginType(ref LoginCredentialType type)
        {
#if UNITY_EDITOR
            type = LoginCredentialType.AccountPortal;  // Default in editor
#elif UNITY_SWITCH
            type = LoginCredentialType.PersistentAuth; // Default for Switch
#elif UNITY_PS4 || UNITY_PS5 || UNITY_GAMECORE
            type = LoginCredentialType.ExternalAuth;   // Default for PS and Xbox consoles
#else
            type = LoginCredentialType.AccountPortal;  // Default on other platforms
#endif
        }

        private void CacheIdInputField(string value)
        {
            IdGlobalCache = value;
        }

        private void CacheTokenField(string value)
        {
            TokenGlobalCache = value;
        }

        public void OnDemoSceneChange(int value)
        {
            string sceneName = SceneSwitcherDropDown.options[value]?.text;
            if (string.IsNullOrWhiteSpace(sceneName) || value == 0)
            {
                return;
            }
            sceneName = sceneName.Replace(" ", "").Replace("&", "And");
            Debug.LogFormat("UILoginMenu (OnDemoSceneChanged): value = {0}", sceneName);
            SceneManager.LoadScene(sceneName);
        }

        public void OnDropdownChange(int value)
        {
            switch (value)
            {
                case 1:
                    loginType = LoginCredentialType.AccountPortal;
                    ConfigureUIForAccountPortalLogin();
                    break;
                case 2:
                    loginType = LoginCredentialType.PersistentAuth;
                    ConfigureUIForPersistentLogin();
                    break;
                case 3:
                    loginType = LoginCredentialType.ExternalAuth;
                    ConfigureUIForExternalAuth();
                    break;
                case 4:
                    loginType = LoginCredentialType.ExchangeCode;
                    break;
                case 5:
                    loginType = ConnectType;
                    break;
                case 0:
                default:
                    loginType = LoginCredentialType.Developer;
                    ConfigureUIForDevAuthLogin();
                    break;
            }

            if (loginType == ConnectType)
            {
                externalCredentialType = GetConnectType();
            }
            else
            {
                externalCredentialType = invalidCredentialType;
            }

            ConfigureUIForLogin();
        }

        public void OnConnectDropdownChange()
        {
            if (loginType != ConnectType)
            {
                return;
            }

            externalCredentialType = GetConnectType();
            ConfigureUIForLogin();
        }

        private ExternalCredentialType GetConnectType()
        {
            string typeName = connectTypeDropdown.options[connectTypeDropdown.value].text;
            if (Enum.TryParse(typeName, out ExternalCredentialType externalType))
            {
                return externalType;
            }
            else
            {
                return invalidCredentialType;
            }
        }

        public void Start()
        {
            _OriginalloginButtonText = loginButtonText.text;
            SetLoginOptionsDropdown();
            ConfigureUIForLogin();
        }

        private void EnterPressedToLogin()
        {
            if (loginButton.IsActive())
            {
                OnLoginButtonClick();
            }
        }

#if ENABLE_INPUT_SYSTEM
        public void Update()
        {
            EventSystem system = EventSystem.current;

            var keyboard = Keyboard.current;

            if (system.currentSelectedGameObject != null && system.currentSelectedGameObject != lastSelectedGameObject)
            {
                lastSelectedGameObject = system.currentSelectedGameObject;
            }

            // Prevent deselection by either reselecting the previously selected or the first selected in EventSystem
            if (system.currentSelectedGameObject == null || system.currentSelectedGameObject?.activeInHierarchy == false)
            {
                // Make sure the selected object is still visible. If it's hidden, then don't select an invisible object.
                if (lastSelectedGameObject == null || lastSelectedGameObject?.activeInHierarchy == false)
                {
                    lastSelectedGameObject = system.firstSelectedGameObject;
                }

                system.SetSelectedGameObject(lastSelectedGameObject);
            }

            // Disable game input if Overlay is visible and has exclusive input
            if (system != null && system.sendNavigationEvents != !EOSManager.Instance.IsOverlayOpenWithExclusiveInput())
            {
                if (EOSManager.Instance.IsOverlayOpenWithExclusiveInput())
                {
                    Debug.LogWarning("UILoginMenu (Update): Game Input (sendNavigationEvents) Disabled.");
                    EventSystem.current.sendNavigationEvents = false;
                    EventSystem.current.currentInputModule.enabled = false;
                    return;
                }
                else
                {
                    Debug.Log("UILoginMenu (Update): Game Input (sendNavigationEvents) Enabled.");
                    EventSystem.current.sendNavigationEvents = true;
                    EventSystem.current.currentInputModule.enabled = true;
                }
            }

            if (keyboard != null
                && system.currentSelectedGameObject != null)
            {
                // Tab between input fields
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    Selectable next = system.currentSelectedGameObject.GetComponent<Selectable>().FindSelectableOnDown();
                    if (keyboard.shiftKey.isPressed)
                    {
                        next = system.currentSelectedGameObject.GetComponent<Selectable>().FindSelectableOnUp();
                    }

                    // Make sure the object is active or exit out if no more objects are found
                    while (next != null && !next.gameObject.activeSelf)
                    {
                        next = next.FindSelectableOnDown();
                    }

                    if (next != null)
                    {
                        InputField inputField = next.GetComponent<InputField>();
                        UIConsoleInputField consoleInputField = next.GetComponent<UIConsoleInputField>();
                        if (inputField != null)
                        {
                            inputField.OnPointerClick(new PointerEventData(system));
                            system.SetSelectedGameObject(next.gameObject);
                        }
                        else if (consoleInputField != null)
                        {
                            consoleInputField.InputField.OnPointerClick(new PointerEventData(system));
                            system.SetSelectedGameObject(consoleInputField.InputField.gameObject);
                        }
                        else
                        {
                            system.SetSelectedGameObject(next.gameObject);
                        }
                    }
                    else
                    {
                        next = FindTopUISelectable();
                        system.SetSelectedGameObject(next.gameObject);
                    }
                }
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    // Enter pressed in an input field
                    InputField inputField = system.currentSelectedGameObject.GetComponent<InputField>();
                    UIConsoleInputField consoleInputField = system.currentSelectedGameObject.GetComponent<UIConsoleInputField>();
                    if (inputField != null || consoleInputField != null)
                    {
                        EnterPressedToLogin();
                    }
                }
            }

            // Controller: Detect if nothing is selected and controller input detected, and set default
            bool nothingSelected = system != null && system.currentSelectedGameObject == null;
            bool inactiveButtonSelected = system != null && system.currentSelectedGameObject != null && !system.currentSelectedGameObject.activeInHierarchy;

            var gamepad = Gamepad.current;
            if ((nothingSelected || inactiveButtonSelected)
                && gamepad != null && gamepad.wasUpdatedThisFrame)
            {
                if (UIFirstSelected.activeSelf == true)
                {
                    system.SetSelectedGameObject(UIFirstSelected);
                }
                else if (UIFindSelectable?.activeSelf == true)
                {
                    system.SetSelectedGameObject(UIFindSelectable);
                }
                else
                {
                    var selectables = GameObject.FindObjectsOfType<Selectable>(false);
                    foreach (var selectable in selectables)
                    {
                        if (selectable.navigation.mode != Navigation.Mode.None)
                        {
                            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                            break;
                        }
                    }
                }

                Debug.Log("Nothing currently selected, default to UIFirstSelected: system.currentSelectedGameObject = " + system.currentSelectedGameObject);
            }
            signInWithAppleManager?.Update();
        }
#else

        /// <summary>
        /// Stores a reference to the last game object that was selected.
        /// </summary>
        private GameObject lastSelectedGameObject = null;

        /// <summary>
        /// If no GameObject is selected, but one was selected before - then determines if the
        /// de-selected GameObject should actually still be detected. If so, brings focus back
        /// to the previously selected game object. If not, then the first selected GameObject
        /// is focused. Broadly speaking, this prevents a focus state wherein no GameObject has
        /// focus.
        /// </summary>
        /// <param name="previouslySelectedGameObject">Reference to the GameObject that had focus on the last update.</param>
        private static void PreventDeselection(ref GameObject previouslySelectedGameObject)
        {
            // Prevent Deselection
            if (EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject != previouslySelectedGameObject)
            {
                previouslySelectedGameObject = EventSystem.current.currentSelectedGameObject;
            }
            else if (EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject.activeInHierarchy == false)
            {
                // If there is no selected object, or if the currently selected object is not visible.
                if (previouslySelectedGameObject == null || previouslySelectedGameObject.activeInHierarchy == false)
                {
                    // Then set the currently selected object to be the first selected game object.
                    previouslySelectedGameObject = EventSystem.current.firstSelectedGameObject;
                }

                EventSystem.current.SetSelectedGameObject(previouslySelectedGameObject);
            }
        }

        /// <summary>
        /// Determines whether input should be passed to the scene, or if it should be skipped.
        /// This is in-order to prevent input from being handled when the EOS overlay is active.
        /// </summary>
        /// <returns>True if input should be handled, false if not.</returns>
        private static bool ShouldInputBeHandled()
        {
#if UNITY_PS4 || UNITY_PS5
            // Disable game input if Overlay is visible and has exclusive input
            if (EventSystem.current != null && EventSystem.current.sendNavigationEvents != !EOSManager.Instance.IsOverlayOpenWithExclusiveInput())
            {
                if (EOSManager.Instance.IsOverlayOpenWithExclusiveInput())
                {
                    Debug.LogWarning("UILoginMenu (Update): Game Input (sendNavigationEvents) Disabled.");
                    EventSystem.current.sendNavigationEvents = false;
                    return false;
                }
                else
                {
                    Debug.Log("UILoginMenu (Update): Game Input (sendNavigationEvents) Enabled.");
                    EventSystem.current.sendNavigationEvents = true;
                    return true;
                }
            }
#else
            return true;
#endif
        }

        /// <summary>
        /// Determines which GameObject should be selected.
        /// </summary>
        /// <param name="firstGameObject">Reference to the GameObject that is considered to be the "first" in tab focus order.</param>
        /// <param name="findSelectable">Reference to the GameObject that can be used to find other selectables if doing so is necessary.</param>
        private static void SetSelectedGameObject(ref GameObject firstGameObject, ref GameObject findSelectable)
        {
            // Determine if nothing is selected
            bool nothingSelected = EventSystem.current.currentSelectedGameObject == null;

            // Is an inactive button currently selected? (More accurately: Is there a currently selected object, and is that currently selected object invisible?)
            bool inactiveButtonSelected = EventSystem.current.currentSelectedGameObject != null && !EventSystem.current.currentSelectedGameObject.activeInHierarchy;

            if ((nothingSelected || inactiveButtonSelected)
                && (Input.GetAxis("Horizontal") != 0.0f || Input.GetAxis("Vertical") != 0.0f))
            {
                if (firstGameObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(firstGameObject);
                }
                else if (findSelectable.activeSelf == true)
                {
                    EventSystem.current.SetSelectedGameObject(findSelectable);
                }
                else
                {
                    var nextSelectable = FindObjectsOfType<Selectable>(false)
                        .FirstOrDefault(s => s.navigation.mode != Navigation.Mode.None);
                    if (null != nextSelectable)
                    {
                        EventSystem.current.SetSelectedGameObject(nextSelectable.gameObject);
                    }
                }

                Debug.Log($"Nothing currently selected, default to UIFirstSelected: system.currentSelectedGameObject = \"{EventSystem.current.currentSelectedGameObject}\".");
            }
        }

        private static void SetFocusedGameObject(ref GameObject previouslySelected, ref GameObject firstSelected,
            ref GameObject findSelectable)
        {
            // Prevents game object from being de-selected.
            PreventDeselection(ref previouslySelected);

            // Determines whether or not to handle the input (typically not if the EOS overlay is active).
            // If input should not be handled, then the process stops here.
            if (!ShouldInputBeHandled()) { return; }

            // Determine which object should be selected and make sure that it is.
            SetSelectedGameObject(ref firstSelected, ref findSelectable);

            // If tab has been pressed, progress the selected control to the next one.
            HandleTabInput();
        }

        private static void HandleTabInput()
        {
            // Stop handling if Tab is not pressed, or if there is no currently selected game object.
            if (!Input.GetKeyDown(KeyCode.Tab) || null == EventSystem.current.currentSelectedGameObject)
            {
                return;
            }

            Selectable next = EventSystem.current.currentSelectedGameObject
                .GetComponent<Selectable>().FindSelectableOnDown();

            if (next != null)
            {
                // If the "next" control getting focus has an input field component.
                if (next.TryGetComponent<InputField>(out var inputField))
                {
                    // Then simulate a pointer click on that component.
                    inputField.OnPointerClick(new PointerEventData(EventSystem.current));
                }
            }
            else
            {
                // Find the navigable selectable with the highest y position (highest on the
                // screen), and set "next" to that selectable.
                next = Selectable.allSelectablesArray
                    .Where(selectable => selectable.navigation.mode != Navigation.Mode.None)
                    .OrderByDescending(selectable => selectable.transform.position.y)
                    .FirstOrDefault();
            }

            // If a "next" control has been found, then set the selected game object to the
            // game object associated with it.
            if (next != null)
            {
                EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
        }

        public void Update()
        {
            // Set focus to the appropriate game object.
            SetFocusedGameObject(ref lastSelectedGameObject, ref UIFirstSelected, ref UIFindSelectable);

            // If the apple sign-in manager exists, then update it as well.
            if (null != signInWithAppleManager)
            {
                signInWithAppleManager.Update();
            }
        }
#endif

        #region Functions to Configure The UI for different types of authentication.

        private void ConfigureUIForDevAuthLogin()
        {
            loginTypeDropdown.value = loginTypeDropdown.options.FindIndex(option => option.text == "Dev Auth");

            if (!string.IsNullOrEmpty(IdGlobalCache))
            {
                idInputField.InputField.text = IdGlobalCache;
            }

            if (!string.IsNullOrEmpty(TokenGlobalCache))
            {
                tokenInputField.InputField.text = TokenGlobalCache;
            }

            idContainer.gameObject.SetActive(true);
            connectTypeContainer.gameObject.SetActive(false);
            idInputField.gameObject.SetActive(true);
            tokenInputField.gameObject.SetActive(true);
            idText.gameObject.SetActive(true);
            tokenText.text = "Username";
            tokenTooltip.Text = "Username configured in EOS Dev Auth Tool";
            tokenText.gameObject.SetActive(true);
            removePersistentTokenButton.gameObject.SetActive(false);

            tokenInputField.InputFieldButton.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = idInputField.InputFieldButton,
                selectOnDown = loginButton
            };

            loginTypeDropdown.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = SceneSwitcherDropDown,
                selectOnDown = idInputField.InputFieldButton
            };
            
            loginButton.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = tokenInputField.InputFieldButton,
                selectOnDown = logoutButton,
                selectOnLeft = logoutButton
            };
        }

        private void ConfigureUIForAccountPortalLogin()
        {
            loginTypeDropdown.value = loginTypeDropdown.options.FindIndex(option => option.text == "Account Portal");

            idContainer.gameObject.SetActive(true);
            connectTypeContainer.gameObject.SetActive(false);
            idInputField.gameObject.SetActive(false);
            tokenInputField.gameObject.SetActive(false);
            idText.gameObject.SetActive(false);
            tokenText.gameObject.SetActive(false);
            removePersistentTokenButton.gameObject.SetActive(false);

            loginTypeDropdown.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = SceneSwitcherDropDown,
                selectOnDown = loginButton
            };

            loginButton.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loginTypeDropdown,
                selectOnDown = logoutButton,
                selectOnLeft = logoutButton
            };

            // AC/TODO: Reduce duplicated UI code for the different login types
            SceneSwitcherDropDown.gameObject.SetActive(true);
            DemoTitle.gameObject.SetActive(true);
            loginTypeDropdown.gameObject.SetActive(true);

            loginButtonText.text = _OriginalloginButtonText;
            if (PreventLogIn != null)
                StopCoroutine(PreventLogIn);
            loginButton.enabled = true;
            loginButton.gameObject.SetActive(true);
            logoutButton.gameObject.SetActive(false);

            EventSystem.current.SetSelectedGameObject(UIFirstSelected);
        }

        private void ConfigureUIForPersistentLogin()
        {
            loginTypeDropdown.value = loginTypeDropdown.options.FindIndex(option => option.text == "PersistentAuth");

            idContainer.gameObject.SetActive(true);
            connectTypeContainer.gameObject.SetActive(false);
            idInputField.gameObject.SetActive(false);
            tokenInputField.gameObject.SetActive(false);
            idText.gameObject.SetActive(false);
            tokenText.gameObject.SetActive(false);
            removePersistentTokenButton.gameObject.SetActive(true);

            loginTypeDropdown.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = SceneSwitcherDropDown,
                selectOnDown = loginButton
            };

            loginButton.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loginTypeDropdown,
                selectOnDown = logoutButton,
                selectOnLeft = logoutButton
            };
        }

        //-------------------------------------------------------------------------
        private void ConfigureUIForExternalAuth()
        {
            loginTypeDropdown.value = loginTypeDropdown.options.FindIndex(option => option.text == "ExternalAuth");

            idContainer.gameObject.SetActive(true);
            connectTypeContainer.gameObject.SetActive(false);
            idInputField.gameObject.SetActive(false);
            tokenInputField.gameObject.SetActive(false);
            idText.gameObject.SetActive(false);
            tokenText.gameObject.SetActive(false);
            removePersistentTokenButton.gameObject.SetActive(false);

            loginTypeDropdown.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = SceneSwitcherDropDown,
                selectOnDown = loginButton
            };

            loginButton.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loginTypeDropdown,
                selectOnDown = logoutButton,
                selectOnLeft = logoutButton
            };
        }

        private void ConfigureUIForExchangeCode()
        {
            loginTypeDropdown.value = loginTypeDropdown.options.FindIndex(option => option.text == "ExchangeCode");

            idContainer.gameObject.SetActive(true);
            connectTypeContainer.gameObject.SetActive(false);
            idInputField.gameObject.SetActive(false);
            tokenInputField.gameObject.SetActive(false);
            idText.gameObject.SetActive(false);
            tokenText.gameObject.SetActive(false);
            removePersistentTokenButton.gameObject.SetActive(false);

            loginTypeDropdown.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = SceneSwitcherDropDown,
                selectOnDown = loginButton
            };

            loginButton.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loginTypeDropdown,
                selectOnDown = logoutButton,
                selectOnLeft = logoutButton
            };
        }

        private void ConfigureUIForConnectLogin()
        {
            idContainer.gameObject.SetActive(false);
            connectTypeContainer.gameObject.SetActive(true);

            tokenInputField.gameObject.SetActive(false);
            tokenText.gameObject.SetActive(false);
            removePersistentTokenButton.gameObject.SetActive(false);

            // NOTE: Might need to check this better. Currently, using ifdefs to turn on or off platform
            //       specific cases that determine whether or not the login button is enabled.
            switch (externalCredentialType)
            {

                //case ExternalCredentialType.GogSessionTicket:
                //case ExternalCredentialType.GoogleIdToken:
                //case ExternalCredentialType.ItchioJwt:
                //case ExternalCredentialType.ItchioKey:
                //case ExternalCredentialType.AmazonAccessToken:

#if !(UNITY_STANDALONE)
                case ExternalCredentialType.SteamSessionTicket:
                case ExternalCredentialType.SteamAppTicket:
                    loginButton.interactable = false;
                    loginButtonText.text = "Platform not set up.";
                    break;
#endif
#if !(UNITY_STANDALONE || UNITY_ANDROID)
                case ExternalCredentialType.OculusUseridNonce:
                    loginButton.interactable = false;
                    loginButtonText.text = "Platform not set up.";
                    break;
#endif
#if !(UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS)
                case ExternalCredentialType.DiscordAccessToken:
                case ExternalCredentialType.OpenidAccessToken:
                    loginButton.interactable = false;
                    loginButtonText.text = "Platform not set up.";
                    break;
#endif
#if !(UNITY_IOS || UNITY_STANDALONE_OSX)
                case ExternalCredentialType.AppleIdToken:
                    loginButton.interactable = false;
                    loginButtonText.text = "Platform not supported.";
                    break;
#endif
                case ExternalCredentialType.DeviceidAccessToken:
                default:
                    break;
            }

            if (externalCredentialType == ExternalCredentialType.OpenidAccessToken)
            {
                tokenText.text = "Credentials";
                tokenTooltip.Text = "Credentials for OpenID login sample in the form of username:password";
                tokenInputField.gameObject.SetActive(true);
                tokenText.gameObject.SetActive(true);

                connectTypeDropdown.navigation = new Navigation()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = loginTypeDropdown,
                    selectOnDown = tokenInputField.InputFieldButton,
                    selectOnLeft = logoutButton
                };

                loginButton.navigation = new Navigation()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = tokenInputField.InputFieldButton,
                    selectOnDown = logoutButton,
                    selectOnLeft = logoutButton
                };

                tokenInputField.InputFieldButton.navigation = new Navigation()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = connectTypeDropdown,
                    selectOnDown = loginButton
                };
            }
            else
            {
                connectTypeDropdown.navigation = new Navigation()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = loginTypeDropdown,
                    selectOnDown = loginButton,
                    selectOnLeft = logoutButton
                };

                loginButton.navigation = new Navigation()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = connectTypeDropdown,
                    selectOnDown = logoutButton,
                    selectOnLeft = logoutButton
                };
            }

            loginTypeDropdown.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = SceneSwitcherDropDown,
                selectOnDown = connectTypeDropdown
            };

            
        }

        #endregion

        private void ConfigureUIForLogin()
        {
            OnLogout?.Invoke();

            SceneSwitcherDropDown.gameObject.SetActive(true);
            DemoTitle.gameObject.SetActive(true);
            loginTypeDropdown.gameObject.SetActive(true);

            loginButtonText.text = _OriginalloginButtonText;
            if (PreventLogIn != null)
                StopCoroutine(PreventLogIn);
            loginButton.enabled = true;
            loginButton.interactable = true;
            loginButton.gameObject.SetActive(true);
            logoutButton.gameObject.SetActive(false);

            switch (loginType)
            {
                case LoginCredentialType.AccountPortal:
                    ConfigureUIForAccountPortalLogin();
                    break;
                case LoginCredentialType.PersistentAuth:
                    ConfigureUIForPersistentLogin();
                    break;
                case LoginCredentialType.ExternalAuth:
                    ConfigureUIForExternalAuth();
                    break;
                case LoginCredentialType.ExchangeCode:
                    ConfigureUIForExchangeCode();
                    break;
                case ConnectType:
                    ConfigureUIForConnectLogin();
                    break;
                case LoginCredentialType.Developer:
                default:
                    ConfigureUIForDevAuthLogin();
                    break;
            }

            // Controller
            EventSystem.current.SetSelectedGameObject(UIFirstSelected);
        }

        private void ConfigureUIForLogout()
        {
            SceneSwitcherDropDown.gameObject.SetActive(false);
            DemoTitle.gameObject.SetActive(false);
            loginTypeDropdown.gameObject.SetActive(false);

            loginButton.gameObject.SetActive(false);
            logoutButton.gameObject.SetActive(true);
            removePersistentTokenButton.gameObject.SetActive(false);

            idText.gameObject.SetActive(false);
            tokenText.gameObject.SetActive(false);
            idInputField.gameObject.SetActive(false);
            tokenInputField.gameObject.SetActive(false);
            connectTypeContainer.gameObject.SetActive(false);

            OnLogin?.Invoke();
        }

        private void SetLoginOptionsDropdown()
        {
            List<Dropdown.OptionData> connectOptions = new();

            // NOTE: All implemented types of login types are listed here, so that
            //       users can see options across all platforms. Use the configure
            //       ConnectType method to turn off access where necessary.
            List<ExternalCredentialType> credentialTypes = new()
            {
                ExternalCredentialType.DeviceidAccessToken,
                ExternalCredentialType.AppleIdToken,
                //ExternalCredentialType.GogSessionTicket,
                //ExternalCredentialType.GoogleIdToken,
                ExternalCredentialType.OculusUseridNonce,
                //ExternalCredentialType.ItchioJwt,
                //ExternalCredentialType.ItchioKey,
                //ExternalCredentialType.AmazonAccessToken
                ExternalCredentialType.SteamSessionTicket,
                ExternalCredentialType.SteamAppTicket,
                ExternalCredentialType.DiscordAccessToken,
                ExternalCredentialType.OpenidAccessToken,
            };

            foreach (ExternalCredentialType type in credentialTypes)
            {
                connectOptions.Add(new Dropdown.OptionData() { text = type.ToString() });
            }

            connectTypeDropdown.options = connectOptions;
        }

        public void OnLogoutButtonClick()
        {
            // if the readme is open, then close it.
            UIReadme readme = UIReadme.FindObjectOfType<UIReadme>();
            readme?.CloseReadme();

            if (EOSManager.Instance.GetLocalUserId() == null)
            {
                EOSManager.Instance.ClearConnectId(EOSManager.Instance.GetProductUserId());
                ConfigureUIForLogin();
                return;
            }

            EOSManager.Instance.StartLogout(EOSManager.Instance.GetLocalUserId(), (ref LogoutCallbackInfo data) => {
                if (data.ResultCode == Result.Success)
                {
#if (UNITY_PS4 || UNITY_PS5) && !UNITY_EDITOR
#if UNITY_PS4
                    var psnManager = EOSManager.Instance.GetOrCreateManager<EOSPSNManagerPS4>();
#elif UNITY_PS5
                    var psnManager = EOSManager.Instance.GetOrCreateManager<EOSPSNManagerPS5>();
#endif

                    ///TODO: Use activating controller index here
                    psnManager.StartLogoutWithPSN(0);
#endif
                    print("Logout Successful. [" + data.ResultCode + "]");
                    ConfigureUIForLogin();
                }

            });
        }

        private IEnumerator TurnButtonOnAfter15Sec()
        {
            for (int i = 15; i >= 0; i--)
            {
                yield return new WaitForSecondsRealtime(1);
                loginButtonText.text = _OriginalloginButtonText + " (" + i + ")";
            }
            loginButton.enabled = true;
            loginButtonText.text = _OriginalloginButtonText;
        }

        //-------------------------------------------------------------------------
        private void StartLoginWithSteam()
        {
            var steamManager = Steam.SteamManager.Instance;
            string steamId = steamManager?.GetSteamID();
            string steamToken = steamManager?.GetSessionTicket();
            if(steamId == null)
            {
                Debug.LogError("ExternalAuth failed: Steam ID not valid");
            }
            else if (steamToken == null)
            {
                Debug.LogError("ExternalAuth failed: Steam session ticket not valid");
            }
            else
            {
                EOSManager.Instance.StartLoginWithLoginTypeAndToken(
                        LoginCredentialType.ExternalAuth,
                        ExternalCredentialType.SteamSessionTicket,
                        steamId,
                        steamToken,
                        StartLoginWithLoginTypeAndTokenCallback);
            }
        }


        //-------------------------------------------------------------------------
        // Username and password aren't always the username and password
        public void OnLoginButtonClick()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogError("Internet not reachable.");
                return;
            }
            
            loginButton.enabled = false;
            if(PreventLogIn!=null)
                StopCoroutine(PreventLogIn);
            PreventLogIn = StartCoroutine(TurnButtonOnAfter15Sec());

            print("Attempting to login...");

            switch (loginType)
            {
                case ConnectType:
                    AcquireTokenForConnectLogin(externalCredentialType);
                    break;
                case LoginCredentialType.ExternalAuth:
#if (UNITY_PS4 || UNITY_PS5) && !UNITY_EDITOR
#if UNITY_PS4
                    var psnManager = EOSManager.Instance.GetOrCreateManager<EOSPSNManagerPS4>();
#elif UNITY_PS5
                    var psnManager = EOSManager.Instance.GetOrCreateManager<EOSPSNManagerPS5>();
#endif

                    ///TOO(mendsley): Use activating controller index here
                    psnManager.StartLoginWithPSN(0, StartLoginWithLoginTypeAndTokenCallback);
#elif UNITY_SWITCH && !UNITY_EDITOR
                    var nintendoManager = EOSManager.Instance.GetOrCreateManager<EOSNintendoManager>();
                    nintendoManager.StartLoginWithNSAPreselectedUser(StartLoginWithLoginTypeAndTokenCallback);
#elif UNITY_GAMECORE && !UNITY_EDITOR
                    EOSXBLManager xblManager = EOSManager.Instance.GetOrCreateManager<EOSXBLManager>();
                    xblManager.StartLoginWithXbl(StartLoginWithLoginTypeAndTokenCallback);
#else
                    Steam.SteamManager.Instance.StartLoginWithSteam(StartLoginWithLoginTypeAndTokenCallback);
#endif
                    break;
                case LoginCredentialType.PersistentAuth:
#if UNITY_SWITCH && !UNITY_EDITOR
                    var nintendoManager = EOSManager.Instance.GetOrCreateManager<EOSNintendoManager>();
                    nintendoManager.StartLoginWithPersistantAuthPreselectedUser((LoginCallbackInfo callbackInfo) =>
                    {
                        if (callbackInfo.ResultCode == Result.Success)
                        {
                            ConfigureUIForLogout();
                        }
                        else
                        {
                            ConfigureUIForLogin();
                        }
                    });
#else
                    EOSManager.Instance.StartPersistentLogin(callbackInfo =>
                    {
                        // In this state, it means one needs to login in again with the previous login type, or a new one, as the
                        // tokens are invalid
                        if (callbackInfo.ResultCode != Epic.OnlineServices.Result.Success)
                        {
                            print("Failed to login with Persistent token [" + callbackInfo.ResultCode + "]");
                            // Other platforms: Fallback to DevAuth login flow
                            loginType = LoginCredentialType.Developer;
                            ConfigureUIForDevAuthLogin();
                        }
                        else
                        {
                            StartLoginWithLoginTypeAndTokenCallback(callbackInfo);
                        }
                    });
#endif
                    break;
                case LoginCredentialType.ExchangeCode:
                    EOSManager.Instance.StartLoginWithLoginTypeAndToken(loginType,
                        null,
                        EOSManager.Instance.GetCommandLineArgsFromEpicLauncher().authPassword,
                        StartLoginWithLoginTypeAndTokenCallback);
                    break;
                case LoginCredentialType.Developer:
                    {
                        string usernameAsString = idInputField.InputField.text.Trim();
                        string passwordAsString = tokenInputField.InputField.text.Trim();

                        if (string.IsNullOrEmpty(usernameAsString))
                        {
                            print("Username is missing");
                            return;
                        }

                        if (string.IsNullOrEmpty(passwordAsString))
                        {
                            print("Password is missing.");
                            return;
                        }

                        // Deal with other EOS log in issues
                        EOSManager.Instance.StartLoginWithLoginTypeAndToken(loginType,
                            usernameAsString,
                            passwordAsString,
                            StartLoginWithLoginTypeAndTokenCallback);
                        break;
                    }
            }
        }

        //-------------------------------------------------------------------------

        private void AcquireTokenForConnectLogin(ExternalCredentialType externalType)
        {
            switch (externalType)
            {
                case ExternalCredentialType.SteamSessionTicket:
                    ConnectSteamSessionTicket();
                    break;

                case ExternalCredentialType.SteamAppTicket:
                    ConnectSteamAppTicket();
                    break;

                case ExternalCredentialType.DeviceidAccessToken:
                    ConnectDeviceId();
                    break;

                case ExternalCredentialType.AppleIdToken:
                    ConnectAppleId();
                    break;

                case ExternalCredentialType.DiscordAccessToken:
                    ConnectDiscord();
                    break;

                case ExternalCredentialType.OpenidAccessToken:
                    ConnectOpenId();
                    break;

                case ExternalCredentialType.OculusUseridNonce:
                    ConnectOculus();
                    break;

                default:
                    if (externalType == invalidCredentialType)
                    {
                        Debug.LogError($"Connect type not valid");
                    }
                    else
                    {
                        Debug.LogError($"Connect Login for {externalType} not implemented");
                    }
                    loginButton.interactable = true;
                    break;
            }
        }

        private void ConnectSteamSessionTicket()
        {
            Steam.SteamManager.Instance.StartConnectLoginWithSteamSessionTicket(ConnectLoginTokenCallback);
        }

        private void ConnectSteamAppTicket()
        {
            Steam.SteamManager.Instance.StartConnectLoginWithSteamAppTicket(ConnectLoginTokenCallback);
        }

        private void ConnectDeviceId()
        {
            var connectInterface = EOSManager.Instance.GetEOSConnectInterface();
            var options = new Epic.OnlineServices.Connect.CreateDeviceIdOptions()
            {
                DeviceModel = SystemInfo.deviceModel
            };

            //connectInterface.CreateDeviceId(ref options, null, CreateDeviceCallback);
            connectInterface.CreateDeviceId(ref options, null, (ref CreateDeviceIdCallbackInfo callbackInfo) =>
            {
                if (callbackInfo.ResultCode == Result.Success || callbackInfo.ResultCode == Result.DuplicateNotAllowed)
                {
                    //this may return "Unknown" on some platforms
                    string displayName = Environment.UserName;
                    EOSManager.Instance.StartConnectLoginWithOptions(ExternalCredentialType.DeviceidAccessToken, null, displayName, ConnectLoginTokenCallback);
                }
                else
                {
                    Debug.LogError("Connect Login failed: Failed to create Device Id");
                    ConfigureUIForLogin();
                }
            });
        }

        private void ConnectAppleId()
        {
            signInWithAppleManager = new Apple.EOSSignInWithAppleManager();
            Debug.Log("Start Connect Login with Apple Id");
            
            signInWithAppleManager.RequestTokenAndUsername((string token,string username) =>
            {
                StartConnectLoginWithToken(ExternalCredentialType.AppleIdToken, token, username.Remove(31));
            });
        }
        
        private void ConnectDiscord()
        {
            if (Discord.DiscordManager.Instance == null)
            {
                Debug.LogError("Connect Login failed: DiscordManager unavailable");
                ConfigureUIForLogin();
                return;
            }

            Discord.DiscordManager.Instance.RequestOAuth2Token((token) =>
            {
                if (token == null)
                {
                    Debug.LogError("Connect Login failed: Unable to get Discord OAuth2 token");
                    ConfigureUIForLogin();
                }
                else
                {
                    EOSManager.Instance.StartConnectLoginWithOptions(
                        ExternalCredentialType.DiscordAccessToken, 
                        token, 
                        onloginCallback: ConnectLoginTokenCallback);
                }
            });
        }

        private void ConnectOpenId()
        {
            var tokenParts = tokenInputField.InputField.text.Split(':');
            if (tokenParts.Length >= 2)
            {
                string username = tokenParts[0].Trim();
                string password = tokenParts[1].Trim();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    OpenId.OpenIdRequestManager.Instance.RequestToken(username, password, 
                        (callbackUsername, callbackToken) =>
                    {
                        if (null == callbackToken)
                        {
                            Debug.LogError("Connect Login filed: Unable to acquire OpenID token.");
                            ConfigureUIForLogin();
                        }
                        else
                        {
                            EOSManager.Instance.StartConnectLoginWithOptions(
                                ExternalCredentialType.OpenidAccessToken, 
                                callbackToken, 
                                onloginCallback: ConnectLoginTokenCallback
                            );
                        }
                    });
                    return;
                }
            }

            Debug.LogError("Connect Login failed: OpenID credentials should be entered as \"username:password\"");
            ConfigureUIForLogin();
        }

        private void ConnectOculus()
        {
            if (Oculus.OculusManager.Instance == null)
            {
                Debug.LogError("Connect Login failed: oculusManager unavailable. Is Oculus setup and available for this platform?");
                ConfigureUIForLogin();
                return;
            }

            Oculus.OculusManager.Instance.GetUserProof((idAndNonce, OculusID) =>
            {
                if (string.IsNullOrEmpty(idAndNonce) || string.IsNullOrEmpty(OculusID))
                {
                    Debug.LogError("Connect Login failed: Unable to get Oculus Proof. Is Oculus setup and available for this platform?");
                    ConfigureUIForLogin();
                }
                else
                {
                    StartConnectLoginWithToken(
                        ExternalCredentialType.OculusUseridNonce,
                        idAndNonce, 
                        OculusID);
                }
            });
        }

        private void StartConnectLoginWithToken(ExternalCredentialType externalType, string token, string displayName = null)
        {
            EOSManager.Instance.StartConnectLoginWithOptions(externalType, token, displayName, ConnectLoginTokenCallback);
        }

        private void ConnectLoginTokenCallback(Epic.OnlineServices.Connect.LoginCallbackInfo connectLoginCallbackInfo)
        {
            if (connectLoginCallbackInfo.ResultCode == Result.Success)
            {
                print("Connect Login Successful. [" + connectLoginCallbackInfo.ResultCode + "]");
                ConfigureUIForLogout();
            }
            else if (connectLoginCallbackInfo.ResultCode == Result.InvalidUser)
            {
                // ask user if they want to ConnectType; sample assumes they do
                EOSManager.Instance.CreateConnectUserWithContinuanceToken(
                    connectLoginCallbackInfo.ContinuanceToken, 
                    createUserCallbackInfo =>
                {
                    print("Creating new ConnectType user");
                    if (createUserCallbackInfo.ResultCode == Result.Success)
                    {
                        ConfigureUIForLogout();
                    }
                    else
                    {
                        ConfigureUIForLogin();
                    }
                });
            }
            else
            {
                ConfigureUIForLogin();
            }
        }

        //-------------------------------------------------------------------------
        private void StartConnectLoginWithLoginCallbackInfo(LoginCallbackInfo loginCallbackInfo)
        {
            EOSManager.Instance.StartConnectLoginWithEpicAccount(loginCallbackInfo.LocalUserId, (Epic.OnlineServices.Connect.LoginCallbackInfo connectLoginCallbackInfo) =>
            {
                if (connectLoginCallbackInfo.ResultCode == Result.Success)
                {
                    print("Connect Login Successful. [" + connectLoginCallbackInfo.ResultCode + "]");
                    ConfigureUIForLogout();
                }
                else if (connectLoginCallbackInfo.ResultCode == Result.InvalidUser)
                {
                    // ask user if they want to ConnectType; sample assumes they do
                    EOSManager.Instance.CreateConnectUserWithContinuanceToken(connectLoginCallbackInfo.ContinuanceToken, (Epic.OnlineServices.Connect.CreateUserCallbackInfo createUserCallbackInfo) =>
                    {
                        print("Creating new ConnectType user");
                        EOSManager.Instance.StartConnectLoginWithEpicAccount(loginCallbackInfo.LocalUserId, (Epic.OnlineServices.Connect.LoginCallbackInfo retryConnectLoginCallbackInfo) =>
                        {
                            if (retryConnectLoginCallbackInfo.ResultCode == Result.Success)
                            {
                                ConfigureUIForLogout();
                            }
                            else
                            {
                                // For any other error, re-enable the login procedure
                                ConfigureUIForLogin();
                            }
                        });
                    });
                }
            });
        }

        //-------------------------------------------------------------------------
        public void StartLoginWithLoginTypeAndTokenCallback(LoginCallbackInfo loginCallbackInfo)
        {
            if (loginCallbackInfo.ResultCode == Epic.OnlineServices.Result.AuthMFARequired)
            {
                // collect MFA
                // do something to give the MFA to the SDK
                print("MFA Authentication not supported in sample. [" + loginCallbackInfo.ResultCode + "]");
            }
            else if (loginCallbackInfo.ResultCode == Result.AuthPinGrantCode)
            {
                ///TODO(mendsley): Handle pin-grant in a more reasonable way
                Debug.LogError("------------PIN GRANT------------");
                Debug.LogError("External account is not connected to an Epic Account. Use link below");
                Debug.LogError($"URL: {loginCallbackInfo.PinGrantInfo?.VerificationURI}");
                Debug.LogError($"CODE: {loginCallbackInfo.PinGrantInfo?.UserCode}");
                Debug.LogError("---------------------------------");
            }
            else if (loginCallbackInfo.ResultCode == Epic.OnlineServices.Result.Success)
            {
                StartConnectLoginWithLoginCallbackInfo(loginCallbackInfo);
            }
            else if (loginCallbackInfo.ResultCode == Epic.OnlineServices.Result.InvalidUser)
            {
                print("Trying Auth link with external account: " + loginCallbackInfo.ContinuanceToken);
                EOSManager.Instance.AuthLinkExternalAccountWithContinuanceToken(loginCallbackInfo.ContinuanceToken, 
#if UNITY_SWITCH
                                                                                LinkAccountFlags.NintendoNsaId,
#else
                                                                                LinkAccountFlags.NoFlags,
#endif
                                                                                (Epic.OnlineServices.Auth.LinkAccountCallbackInfo linkAccountCallbackInfo) =>
                {
                    if (linkAccountCallbackInfo.ResultCode == Result.Success)
                    {
                        StartConnectLoginWithLoginCallbackInfo(loginCallbackInfo);
                    }
                    else
                    {
                        print("Error Doing AuthLink with continuance token in. [" + linkAccountCallbackInfo.ResultCode + "]");
                    }
                });
            }

            else
            {
                print("Error logging in. [" + loginCallbackInfo.ResultCode + "]");
            }

            // Re-enable the login button and associated UI on any error
            if (loginCallbackInfo.ResultCode != Epic.OnlineServices.Result.Success)
            {
                ConfigureUIForLogin();
            }
        }

        public void OnRemovePersistentTokenButtonClick()
        {
            EOSManager.Instance.RemovePersistentToken();
        }

        public void OnExitButtonClick()
        {
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }
    }
}
