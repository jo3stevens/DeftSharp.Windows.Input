﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DeftSharp.Windows.Input.Interceptors;
using DeftSharp.Windows.Input.Native.API;
using DeftSharp.Windows.Input.Native.Keyboard;

namespace DeftSharp.Windows.Input.Keyboard.Interceptors;

internal sealed class KeyboardBinderInterceptor : KeyboardInterceptor
{
    private static readonly Lazy<KeyboardBinderInterceptor> LazyInstance =
        new(() => new KeyboardBinderInterceptor());

    public static KeyboardBinderInterceptor Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<Key, Key> _boundedKeys;

    private Key? _lastProcessedBoundedKey;

    public IReadOnlyDictionary<Key, Key> BoundedKeys => _boundedKeys;

    private KeyboardBinderInterceptor()
        : base(WindowsKeyboardInterceptor.Instance)
    {
        _boundedKeys = new ConcurrentDictionary<Key, Key>();
    }

    public void Bind(Key oldKey, Key newKey)
    {
        if (oldKey == newKey)
            return;

        if (_boundedKeys.ContainsKey(oldKey))
            _boundedKeys[oldKey] = newKey;

        Hook();
        _boundedKeys.TryAdd(oldKey, newKey);
    }

    public void Unbind(Key key)
    {
        if (!_boundedKeys.ContainsKey(key))
            return;

        var boundedKey = _boundedKeys.FirstOrDefault();

        _boundedKeys.TryRemove(boundedKey);

        if (!_boundedKeys.Any())
            Unhook();
    }

    public void UnbindAll()
    {
        var boundedKeys = _boundedKeys.Keys.ToArray();
        foreach (var boundedKey in boundedKeys)
            Unbind(boundedKey);
    }

    public bool IsKeyBounded(Key key) => _boundedKeys.ContainsKey(key);

    public override void Dispose()
    {
        UnbindAll();
        base.Dispose();
    }

    internal override bool OnPipelineUnhookRequested() => !_boundedKeys.Any();

    internal override InterceptorResponse OnKeyboardInput(KeyPressedArgs args)
    {
        return new InterceptorResponse(
            CanBeProcessed(args.KeyPressed),
            new InterceptorInfo(Name, InterceptorType.Binder),
            null, failedInterceptors =>
            {
                if (args.Event == KeyboardEvent.KeyUp)
                    return;

                if (!failedInterceptors.Any(i => i.Name.Equals(Name)))
                    return;
                
                if (!IsKeyBounded(args.KeyPressed))
                    return;

                var key = _boundedKeys[args.KeyPressed];
                _lastProcessedBoundedKey = key;
                KeyboardAPI.Press(key);
            });
    }

    private bool CanBeProcessed(Key pressedKey)
    {
        if (IsKeyBounded(pressedKey) && _lastProcessedBoundedKey != pressedKey)
            return false;

        _lastProcessedBoundedKey = null;
        return true;
    }
}