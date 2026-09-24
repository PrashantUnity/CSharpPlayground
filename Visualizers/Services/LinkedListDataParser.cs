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

    public static VisualizerSequence GenerateCycleDetectionSteps(object? head)
    {
        head = NormalizeHead(head);
        var sequence = new VisualizerSequence();
        var listData = Parse(head);
        var initialStep = new VisualizerStep(0, $"Linked List Loaded • {listData.Nodes.Count} nodes", VisualizerKind.LinkedList)
        {
            Snapshot = WithPointers(listData, 0, 0)
        };
        sequence.AddStep(initialStep);

        if (head == null) return sequence;

        // Floyd's Tortoise and Hare stepping
        object? slow = head;
        object? fast = head;
        int stepCount = 0;
        int maxSteps = 100;

        while (fast != null && stepCount < maxSteps)
        {
            object? nextFast = GetNextNode(fast);
            if (nextFast == null) break;
            fast = GetNextNode(nextFast);
            slow = GetNextNode(slow!);

            stepCount++;
            int slowIdx = FindNodeIndex(listData, slow);
            int fastIdx = FindNodeIndex(listData, fast);

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

            if (slow != null && ReferenceEquals(slow, fast))
            {
                // Meeting point!
                var cycleStep = new VisualizerStep(
                    sequence.TotalSteps,
                    $"🎯 Cycle Confirmed! Slow and Fast pointers met at node [{listData.Nodes.Find(n => n.Index == slowIdx)?.DisplayValue}]",
                    VisualizerKind.LinkedList)
                {
                    Snapshot = WithPointers(listData, slowIdx, fastIdx)
                };
                if (slowIdx >= 0) cycleStep.ActiveNodeIds.Add($"node_{slowIdx}");
                sequence.AddStep(cycleStep);
                break;
            }
        }

        return sequence;
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
