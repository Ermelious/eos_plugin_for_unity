namespace PlayEveryWare.EpicOnlineServices.Samples
{
    using Epic.OnlineServices.Auth;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    public static class DropdownExtensions
    {
        /// <summary>
        /// Returns the enum value selected by a Dropdown, assuming that ToString() is used to
        /// populate the dropdown, and the selected value can be parsed into the indicated enum.
        /// </summary>
        /// <typeparam name="T">The enum that is being displayed in the dropdown.</typeparam>
        /// <param name="dropdown">The dropdown to get the enum value from.</param>
        /// <returns>The value of T selected.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the selected label cannot be parsed into a valid value within the indicated enum.</exception>
        public static T GetSelectedValue<T>(this Dropdown dropdown) where T : struct, Enum
        {
            string selectedLabel = dropdown.options[dropdown.value].text;

            if (Enum.TryParse(selectedLabel, out T type))
            {
                return type;
            }

            string warningText = $"Could not parse string \"{selectedLabel}\" into value of \"{typeof(T).Name}\".";
            Debug.LogWarning(warningText);
            throw new ArgumentOutOfRangeException(warningText);
        }

        public static void Populate<T>(this Dropdown dropdown, List<T> values, T selected, UnityAction<int> onValueChanged)
            where T : struct, Enum
        {
            values.Sort();

            int selectedIndex = values.IndexOf(selected);

            List<Dropdown.OptionData> options = values
                .Select(value => new Dropdown.OptionData(text: value.ToString())).ToList();

            dropdown.options = options;

            if (null != onValueChanged)
            {
                dropdown.onValueChanged.AddListener(onValueChanged);
            }

            dropdown.value = selectedIndex;
        }

        public static void Populate<T>(this Dropdown dropdown, List<T> values, T selected) where T : struct, Enum
        {
            dropdown.Populate(values, selected, null);
        }

        public static void Populate<T>(this Dropdown dropdown, List<T> values) where T : struct, Enum
        {
            values.Sort();
            dropdown.Populate(values, values.FirstOrDefault());
        }

        public static void Populate<T>(this Dropdown dropdown, List<T> values, UnityAction<int> onValueCanged)
            where T : struct, Enum
        {
            values.Sort();
            dropdown.Populate(values, values.FirstOrDefault(), onValueCanged);
        }
    }
}