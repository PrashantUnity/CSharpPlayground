using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class LinkedListDataParser
{
    public static LinkedListData Parse(object? head, int maxNodes = 100)
    {
        var data = new LinkedListData();
        if (head is LinkedListData alreadyList) return alreadyList;

        head = NormalizeHead(head);
        if (head == null) return data;

        if (head is IEnumerable values and not string && !VisualizerReflectionHelper.HasMember(head, "next", "Next"))
        {
            foreach (var value in values)
            {
                if (data.Nodes.Count == maxNodes) break;
                int position = data.Nodes.Count;
                data.Nodes.Add(new LinkedListNodeData(position, value?.ToString() ?? "null", position + 1) { RawValue = value });
            }

            if (data.Nodes.Count > 0) data.Nodes[^1].NextIndex = null;
            return data;
        }

        var visitedPointers = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        var curr = head;
        int index = 0;

        while (curr != null && index < maxNodes)
        {
            if (visitedPointers.TryGetValue(curr, out int existingIndex))
            {
                // Cycle detected!
                data.HasCycle = true;
                data.CycleTargetIndex = existingIndex;
                if (data.Nodes.Count > 0)
                {
                    data.Nodes[^1].NextIndex = existingIndex;
                    data.Nodes[existingIndex].IsCycleTarget = true;
                }
                break;
            }

            visitedPointers[curr] = index;
            string val = ExtractValue(curr);
            var nodeData = new LinkedListNodeData(index, val, index + 1)
            {
                RawValue = curr
            };
            data.Nodes.Add(nodeData);

            curr = GetNextNode(curr);
            index++;
        }

        if (!data.HasCycle && data.Nodes.Count > 0)
        {
            data.Nodes[^1].NextIndex = null; // null terminator
            data.Nodes[^1].ContinuesBeyondView = curr != null;
        }

        return data;
    }

    private const int MaxCycleSteps = 100;

    /// <summary>Floyd's tortoise and hare over the user's own nodes, step by step (it can follow them past the nodes drawn).</summary>
    public static VisualizerSequence GenerateCycleDetectionSteps(object? head)
    {
        head = NormalizeHead(head);
        var listData = Parse(head);
        var sequence = StartCycleDetection(listData, 0);
        if (head == null) return sequence;

        object? slow = head;
        object? fast = head;
        for (int stepCount = 1; fast != null && stepCount <= MaxCycleSteps; stepCount++)
        {
            object? nextFast = GetNextNode(fast);
            if (nextFast == null) break;
            fast = GetNextNode(nextFast);
            slow = GetNextNode(slow!);
            if (AddCycleStep(sequence, listData, stepCount, FindNodeIndex(listData, slow), FindNodeIndex(listData, fast), slow != null && ReferenceEquals(slow, fast)))
            {
                break;
            }
        }

        return sequence;
    }

    /// <summary>Floyd's tortoise and hare over a drawn list's next pointers, from the node at <paramref name="head"/>.</summary>
    public static VisualizerSequence GenerateCycleDetectionSteps(LinkedListData listData, int head = 0)
    {
        var sequence = StartCycleDetection(listData, head);
        if (head < 0 || head >= listData.Nodes.Count) return sequence;

        int? slow = head;
        int? fast = head;
        for (int stepCount = 1; fast != null && stepCount <= MaxCycleSteps; stepCount++)
        {
            int? nextFast = Next(listData, fast.Value);
            if (nextFast == null) break;
            fast = Next(listData, nextFast.Value);
            slow = Next(listData, slow!.Value);
            if (AddCycleStep(sequence, listData, stepCount, slow ?? -1, fast ?? -1, slow != null && slow == fast))
            {
                break;
            }
        }

        return sequence;
    }

    private static int? Next(LinkedListData data, int index) =>
        index >= 0 && index < data.Nodes.Count ? data.Nodes[index].NextIndex : null;

    private static VisualizerSequence StartCycleDetection(LinkedListData listData, int head)
    {
        var sequence = new VisualizerSequence();
        sequence.AddStep(new VisualizerStep(0, $"Linked List Loaded • {listData.Nodes.Count} nodes", VisualizerKind.LinkedList)
        {
            Snapshot = WithPointers(listData, head, head)
        });
        return sequence;
    }

    // One move of both pointers; true when they met (the cycle is confirmed and the walk ends).
    private static bool AddCycleStep(VisualizerSequence sequence, LinkedListData listData, int stepCount, int slowIdx, int fastIdx, bool met)
    {
        var step = new VisualizerStep(
            sequence.TotalSteps,
            $"Step {stepCount}: Slow -> [{listData.Nodes.Find(n => n.Index == slowIdx)?.DisplayValue}], Fast -> [{listData.Nodes.Find(n => n.Index == fastIdx)?.DisplayValue}]",
            VisualizerKind.LinkedList)
        {
            Snapshot = WithPointers(listData, slowIdx, fastIdx)
        };

        if (slowIdx >= 0) step.ActiveNodeIds.Add($"node_{slowIdx}");
        if (fastIdx >= 0) step.ActiveNodeIds.Add($"node_{fastIdx}");
        step.AuxiliaryInfo["Slow Index"] = slowIdx.ToString();
        step.AuxiliaryInfo["Fast Index"] = fastIdx.ToString();
        sequence.AddStep(step);
        if (!met) return false;

        var cycleStep = new VisualizerStep(
            sequence.TotalSteps,
            $"🎯 Cycle Confirmed! Slow and Fast pointers met at node [{listData.Nodes.Find(n => n.Index == slowIdx)?.DisplayValue}]",
            VisualizerKind.LinkedList)
        {
            Snapshot = WithPointers(listData, slowIdx, fastIdx)
        };
        if (slowIdx >= 0) cycleStep.ActiveNodeIds.Add($"node_{slowIdx}");
        sequence.AddStep(cycleStep);
        return true;
    }

    // Each step carries its own copy so the slow and fast badges sit on the right nodes while scrubbing.
    private static LinkedListData WithPointers(LinkedListData data, int slowIndex, int fastIndex)
    {
        var copy = data.Clone();
        copy.Pointers.Add(new PointerMarkerData("slow", slowIndex, VisualizerPaletteService.GetPointerColor("slow")));
        copy.Pointers.Add(new PointerMarkerData("fast", fastIndex, VisualizerPaletteService.GetPointerColor("fast")));
        return copy;
    }

    private static int FindNodeIndex(LinkedListData data, object? nodeObj)
    {
        if (nodeObj == null) return -1;
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            if (ReferenceEquals(data.Nodes[i].RawValue, nodeObj)) return i;
        }
        return -1;
    }

    // A BCL LinkedList<T> is the container; its nodes (with Next/Value) start at First.
    private static object? NormalizeHead(object? head)
    {
        var type = head?.GetType();
        if (type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(LinkedList<>))
        {
            return type.GetProperty(nameof(LinkedList<object>.First))!.GetValue(head);
        }
        return head;
    }

    private static object? GetNextNode(object nodeObj) =>
        VisualizerReflectionHelper.GetMemberValue(nodeObj, "next", "Next");

    private static string ExtractValue(object nodeObj) =>
        VisualizerReflectionHelper.ExtractDisplayValue(nodeObj, "val", "Val", "Value", "value", "Data", "data");
}
