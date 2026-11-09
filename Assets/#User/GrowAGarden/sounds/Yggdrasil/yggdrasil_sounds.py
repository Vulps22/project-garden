"""Synthesises Yggdrasil's sound (docs/yggdrasil.md) as a mono 44.1 kHz WAV beside this script.

- earth_rise.wav  15 s  the tree being built (the build takes 12 s): a rolling, rumbling thunder of moving earth, like an
                        earthbender at work: a sub-bass rumble swelling and rolling, stone grinding, crackling rock and
                        heavy thuds that come faster as the tree rises, settling into a long tail as it is formed.
Run: python3 yggdrasil_sounds.py   (needs numpy and scipy)
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = os.path.dirname(os.path.abspath(__file__))
rng = np.random.default_rng(11)


def t_of(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def band(x, low, high, order=4):
    return sosfilt(butter(order, [low, high], btype="band", fs=RATE, output="sos"), x)


def lowpass(x, cutoff, order=4):
    return sosfilt(butter(order, cutoff, btype="low", fs=RATE, output="sos"), x)


def brown(n):
    """Brown noise: deep and rolling, the body of thunder."""
    x = np.cumsum(rng.standard_normal(n))
    x -= lowpass(x, 8, 2)    # no drift
    return x / np.max(np.abs(x))


def slow_wobble(n, rate, depth, seed):
    """A smooth random swell, so the rumble rolls rather than drones."""
    r = np.random.default_rng(seed)
    points = r.uniform(1 - depth, 1, int(n / RATE * rate) + 3)
    return np.interp(np.arange(n), np.linspace(0, n, len(points)), points)


def thud(length, freq, drop):
    """A heavy impact: a pitched-down sine knock with a burst of dirt on top."""
    t = t_of(length)
    f = freq * np.exp(-t * drop)
    knock = np.sin(2 * np.pi * np.cumsum(f) / RATE) * np.exp(-t * 7)
    dirt = band(rng.standard_normal(len(t)), 120, 900) * np.exp(-t * 18) * 0.5
    return knock + dirt


def earth_rise():
    seconds, build = 15.0, 12.0
    t = t_of(seconds)
    n = len(t)
    rising = np.clip(t / build, 0, 1)
    # Swells in over the build, then the long settle.
    shape = np.minimum(1, t / 1.5) * np.where(t < build, 0.55 + 0.45 * rising, np.exp(-(t - build) * 1.1))

    rumble = lowpass(brown(n), 90) * slow_wobble(n, 2.5, 0.6, 1)
    body = band(brown(n), 60, 220) * slow_wobble(n, 4, 0.7, 2) * 0.6
    # Stone grinding on stone: mid noise, rough, gated by its own slow wobble.
    grind = band(rng.standard_normal(n), 250, 1400) * slow_wobble(n, 7, 0.9, 3) ** 2 * 0.22
    # Crackling rock: sparse grains, thicker as it rises.
    crackle = np.zeros(n)
    for _ in range(900):
        at = rng.uniform(0, build + 1)
        if rng.uniform() > 0.25 + 0.75 * min(1, at / build):
            continue
        i = int(at * RATE)
        g = band(rng.standard_normal(int(0.03 * RATE)), 800, 4000) * np.exp(-np.linspace(0, 9, int(0.03 * RATE)))
        crackle[i:i + len(g)] += g[: max(0, min(len(g), n - i))] * rng.uniform(0.2, 0.6)
    # Heavy thuds, the earth moving in pushes: further apart at first, closer as the tree climbs.
    thuds = np.zeros(n)
    at = 0.4
    while at < build:
        hit = thud(1.6, rng.uniform(48, 62), 1.2) * rng.uniform(0.6, 1.0)
        i = int(at * RATE)
        thuds[i:i + len(hit)] += hit[: max(0, min(len(hit), n - i))]
        at += rng.uniform(1.4, 2.2) * (1 - 0.55 * at / build)
    final = thud(3.5, 40, 0.6) * 1.4    # the tree formed: one last, deepest push
    i = int(build * RATE)
    thuds[i:i + len(final)] += final[: n - i]

    mix = (rumble * 1.0 + body + grind + crackle * 0.35) * shape + thuds * 0.75
    mix = np.tanh(mix * 1.4)    # a little grit, and no clipping
    fade = np.minimum(1, (seconds - t) / 0.4)
    return mix * fade


def write(name, x):
    x = x / np.max(np.abs(x)) * 0.9
    wavfile.write(os.path.join(OUT, name), RATE, (x * 32767).astype(np.int16))
    print(f"wrote {name} {len(x) / RATE:.1f}s")


write("earth_rise.wav", earth_rise())
