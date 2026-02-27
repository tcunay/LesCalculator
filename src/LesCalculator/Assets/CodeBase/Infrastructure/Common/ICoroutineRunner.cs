using System.Collections;
using UnityEngine;

namespace CodeBase.Infrastructure.Common
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator load);
    }
}