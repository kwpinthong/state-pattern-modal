using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ALittleBB.StateMachines.Editor
{
    /// <summary>
    /// Inspector of a <see cref="StateMachineHost"/>: the default fields, then the stack of its machine listed
    /// top → bottom (the first row is the current state). The list follows the machine while the Inspector is open.
    /// The stack section is hidden outside Play mode while the stack is empty, and when several hosts are selected.
    /// </summary>
    [CustomEditor(typeof(StateMachineHost))]
    [CanEditMultipleObjects]
    sealed class StateMachineHostEditor : UnityEditor.Editor
    {
        internal const string StackSectionName = "state-machine-host-stack";
        internal const string StackStateClass = "state-machine-host__stack-state";
        internal const string StackStatusClass = "state-machine-host__stack-status";

        const string Header = "Stack (top → bottom)";
        const string CurrentStatus = "Current";
        const string PausedStatus = "Paused";
        const string EmptyText = "Empty";
        const string DestroyedStateText = "(destroyed StateId)";

        StateMachine _machine;
        VisualElement _section;
        VisualElement _rows;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            // Unity may ask for the content again; only the latest one is kept up to date.
            StopFollowing();
            if (targets.Length == 1)
            {
                root.Add(BuildStackSection());
                _machine = ((StateMachineHost)target).Machine;
                _machine.Changed += OnStackChanged;
                Refresh();
            }

            return root;
        }

        void OnDisable() => StopFollowing();

        void StopFollowing()
        {
            if (_machine != null)
                _machine.Changed -= OnStackChanged;

            _machine = null;
            _section = null;
            _rows = null;
        }

        void OnStackChanged(StateChanged signal) => Refresh();

        VisualElement BuildStackSection()
        {
            _section = new VisualElement { name = StackSectionName };
            _section.style.marginTop = 8;

            var header = new Label(Header);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginLeft = 3;
            header.style.marginBottom = 2;
            _section.Add(header);

            _rows = new VisualElement();
            _section.Add(_rows);
            return _section;
        }

        /// <summary>Rebuilds the rows from <see cref="StateMachine.Stack"/> (bottom → top), last entry first.</summary>
        void Refresh()
        {
            IReadOnlyList<StateId> stack = _machine.Stack;
            int top = stack.Count - 1;

            _section.style.display = EditorApplication.isPlaying || stack.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _rows.Clear();
            for (int i = top; i >= 0; i--)
                _rows.Add(BuildRow(stack[i], isTop: i == top));

            if (stack.Count == 0)
            {
                var empty = new Label(EmptyText);
                empty.style.marginLeft = 3;
                empty.style.opacity = 0.6f;
                _rows.Add(empty);
            }
        }

        static VisualElement BuildRow(StateId id, bool isTop)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 3;

            var status = new Label(isTop ? CurrentStatus : PausedStatus);
            status.AddToClassList(StackStatusClass);
            status.style.width = 64;
            status.style.opacity = 0.6f;
            row.Add(status);

            var state = new Label(id != null ? id.name : DestroyedStateText);
            state.AddToClassList(StackStateClass);
            if (isTop)
                state.style.unityFontStyleAndWeight = FontStyle.Bold;

            row.Add(state);
            return row;
        }
    }
}
