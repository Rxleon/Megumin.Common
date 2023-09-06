﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Megumin
{
    /// <summary>
    /// 重入锁
    /// <para/> 保证一个长时间任务执行期间尝试多次调用，返回相同的任务，不多次开始新任务。
    /// 任务完成后，则可以再次开启新任务。
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    /// <remarks>
    /// <code>
    /// 用例：
    /// A去酒吧点一瓶啤酒，酒保说吧台没酒了，稍等，现在叫库房送来一箱啤酒。
    /// 酒保打电话给库房，要求送一箱啤酒。
    /// B这时来到酒吧也点了一瓶啤酒，酒保说现在吧台没酒了，稍等，库房正在送酒来。
    ///
    /// 此时酒保不需要再次打电话要求库房送酒，刚才已经通知了，现在等结果就可以。
    /// 这就是防止重入机制，在异步任务执行过程中，不要重复进入相同任务。
    ///
    /// 一段时间后，库房送来一箱啤酒。
    /// 酒保先拿给A一瓶啤酒，然后再拿给B一瓶啤酒。
    ///
    /// 特别注意:要保证A和B的回调执行顺序。
    /// </code>
    /// 
    /// </remarks>
    public interface IReEntryLock<K, V>
    {
        /// <summary>
        /// 是否开启
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// 防止重入调用
        /// </summary>
        /// <param name="key">有Lambda闭包，K不能带in标记</param>
        /// <param name="function"></param>
        /// <returns></returns>
        V WrapCall(K key, Func<V> function);
    }

    //function 三种情况，
    //同步函数
    //异步函数同步完成
    //异步函数挂起

    //function函数执行期间，后续调用来自哪个线程
    //与第一次调用相同线程，
    //与第一次调用不同线程，

    //组合有6种情况，有些情况不存在，有些情况不需要保证callback顺序。
    //同步函数           相同线程   不存在
    //异步函数同步完成   相同线程   不存在
    //异步函数挂起       相同线程   需要保证callback顺序
    //同步函数           不同线程   不保证顺序
    //异步函数同步完成   不同线程   不保证顺序
    //异步函数挂起       不同线程   需要保证callback顺序


    //function 三种情况，可以总结为
    //后续调用发生在  function同步执行时，还是 异步挂起时。
    //如果是同步执行时，肯定多线程调用。
    //如果是异步挂起时，可能是多线程，也可能是同一个线程。

    //同步执行时，需要创建一个source，通过source创建task返回。
    //function函数一旦挂起，证明function的结果task可以存在，直接返回即可。

    //function结果的task缓存 和 新创建的source缓存， 有task返回task，无task返回通过Source创建的task。
    //使用source返回的 肯定都是多线程的，不需要保证callback执行顺序。
    //使用Task.Run执行source.TrySetResult，保证不插入后续调用的callback到第一次的callback执行前。

    public class ReEntryLockBase<K, V>
    {
        public bool Enabled { get; set; } = true;
        protected Dictionary<K, TaskCompletionSource<V>> IsRunningSource { get; } = new();
    }


    public class ReEntryLockSync<K, V> : ReEntryLockBase<K, V>, IReEntryLock<K, V>
    {
        public V WrapCall(K key, Func<V> function)
        {
            if (function is null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (Enabled == false)
            {
                return function();
            }

            if (IsRunningSource.TryGetValue(key, out var source))
            {
                return source.Task.Result;
            }
            else
            {
                source = new TaskCompletionSource<V>();
                IsRunningSource[key] = source;

                var result = function();
                Task.Run(() =>
                {
                    source.TrySetResult(result);
                    IsRunningSource.Remove(key);
                });
                return result;
            }
        }
    }

    public class ReEntryLockTask<K, V> : ReEntryLockBase<K, V>, IReEntryLock<K, Task<V>>
    {
        Dictionary<K, Task<V>> IsRunningTask = new();
        public Task<V> WrapCall(K key, Func<Task<V>> function)
        {
            if (function is null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (Enabled == false)
            {
                return function();
            }

            if (IsRunningTask.TryGetValue(key, out var task))
            {
                return task;
            }
            else if (IsRunningSource.TryGetValue(key, out var source))
            {
                return source.Task;
            }
            else
            {
                source = new TaskCompletionSource<V>();
                IsRunningSource[key] = source;

                var resultTask = function();

                if (resultTask.IsCompleted == false)
                {
                    IsRunningTask[key] = resultTask;
                }

                Task.Run(async () =>
                {
                    var result = await resultTask;
                    source.TrySetResult(result);
                    IsRunningSource.Remove(key);
                    IsRunningTask.Remove(key);
                });

                return resultTask;
            }
        }
    }


    public class ReEntryLockValueTask<K, V> : ReEntryLockBase<K, V>, IReEntryLock<K, ValueTask<V>>
    {
        Dictionary<K, ValueTask<V>> IsRunningTask = new();
        public ValueTask<V> WrapCall(K key, Func<ValueTask<V>> function)
        {
            if (function is null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (Enabled == false)
            {
                return function();
            }

            if (IsRunningTask.TryGetValue(key, out var task))
            {
                return task;
            }
            else if (IsRunningSource.TryGetValue(key, out var source))
            {
                return new ValueTask<V>(source.Task);
            }
            else
            {
                source = new TaskCompletionSource<V>();
                IsRunningSource[key] = source;

                var resultTask = function();

                if (resultTask.IsCompleted == false)
                {
                    IsRunningTask[key] = resultTask;
                }

                Task.Run(async () =>
                {
                    var result = await resultTask;
                    source.TrySetResult(result);
                    IsRunningSource.Remove(key);
                    IsRunningTask.Remove(key);
                });

                return resultTask;
            }
        }
    }
}
