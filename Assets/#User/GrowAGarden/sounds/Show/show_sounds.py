"""Synthesises the light show's sounds (docs/moonflower.md) as mono 44.1 kHz WAVs beside this script.

- beam_rise.wav     2.3 s  a Moonflower's beam climbing: a bright tone gliding up two octaves with a shimmer over it,
                           easing out as it reaches the dome (the beam takes 2 s). The bell it once ended on was
                           removed (Vulps).
- dome_drop.wav     6.3 s  night falling over the island: a deep detuned drone bending down an octave with a warble,
                           its tone darkening as it closes (the spread takes 6 s).
- dome_lift.wav     3.2 s  night drawing back: the drone rising and opening, fading away (the retreat takes 3 s).
- converge.wav      3.6 s  a second Moonflower joining: a pad chord swelling as the star glides to the top, with a
                           rising shimmer (the chime it once ended on was removed, Vulps).
Run: python3 show_sounds.py   (needs numpy and scipy)
"""
import os

import numpy as np
from scipy.io import wavfile

RATE = 44100
OUT = os.path.dirname(os.path.abspath(__file__))
rng = np.random.default_rng(7)


def t_of(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def phase(freq):
    """Phase of a tone whose frequency changes over time (an array, one per sample)."""
    return 2 * np.pi * np.cumsum(freq) / RATE


def saw(freq, harmonics=24):
    """A band-limited sawtooth by adding harmonics, each dropped above Nyquist."""
    ph = phase(freq)
    out = np.zeros_like(ph)
    for k in range(1, harmonics + 1):
        out += np.where(freq * k < RATE / 2.2, np.sin(ph * k) / k, 0.0)
    return out * (2 / np.pi)


def lowpass(x, cutoff, q=0.9):
    """A resonant state-variable low-pass whose cutoff may change every sample."""
    cutoff = np.broadcast_to(cutoff, x.shape)
    low = band = 0.0
    out = np.empty_like(x)
    damp = 1.0 / q
    for i in range(len(x)):
        f = 2 * np.sin(np.pi * min(cutoff[i], RATE / 6) / RATE)
        low += f * band
        high = x[i] - low - damp * band
        band += f * high
        out[i] = low
    return out


def envelope(n, attack, release, curve=2.0):
    env = np.ones(n)
    a, r = int(attack * RATE), int(release * RATE)
    if a:
        env[:a] = np.linspace(0, 1, a) ** curve
    if r:
        env[-r:] *= np.linspace(1, 0, r) ** curve
    return env


def bell(t, start, freq, decay):
    """A struck bell: inharmonic partials decaying from start."""
    s = np.clip(t - start, 0, None)
    on = (t >= start).astype(float)
    partials = [(1.0, 1.0), (2.76, 0.5), (5.4, 0.25), (8.93, 0.12)]
    return on * sum(a * np.sin(2 * np.pi * freq * m * s) * np.exp(-s / (decay / m ** 0.5)) for m, a in partials)


def save(name, x, peak_db=-1.0, fade_in=True):
    """Normalises to peak_db and fades the ends against clicks. fade_in=False for a sharp onset: a 10 ms fade-in
    erases a transient."""
    x = x / max(1e-9, np.max(np.abs(x))) * 10 ** (peak_db / 20)
    fade = int(0.01 * RATE)
    if fade_in:
        x[:fade] *= np.linspace(0, 1, fade)
    x[-fade:] *= np.linspace(1, 0, fade)
    wavfile.write(os.path.join(OUT, name), RATE, (x * 32767).astype(np.int16))
    print(f"{name}: {len(x) / RATE:.1f} s")


# ── Beam rise ────────────────────────────────────────────────────────────────
t = t_of(2.3)
climb = np.clip(t / 2.0, 0, 1)
f = 110 * 4 ** (climb ** 1.4)                       # two octaves up, quickening
tone = 0.5 * saw(f, 16) + 0.5 * saw(f * 1.007, 16)  # a slight detune, for width
tone = lowpass(tone, 600 + 5000 * climb ** 1.5, q=2.0)
shimmer = sum(np.sin(phase(f * m) + rng.uniform(0, 6)) for m in (4, 6.02, 8.01)) / 3
shimmer *= (0.5 + 0.5 * np.sin(2 * np.pi * 11 * t)) * climb ** 2
beam = (0.7 * tone + 0.35 * shimmer) * envelope(len(t), 0.08, 0.5)
save("beam_rise.wav", beam)

# ── Dome drop ────────────────────────────────────────────────────────────────
t = t_of(6.3)
fall = np.clip(t / 6.0, 0, 1)
vibrato = 1 + 0.03 * np.sin(2 * np.pi * (5.0 - 3.5 * fall) * t)   # the warb, slowing as it falls
f = 90 * 0.5 ** (fall ** 1.3) * vibrato                            # an octave down
drone = sum(saw(f * d, 30) for d in (1.0, 1.006, 0.994, 2.003)) / 4
sub = np.sin(phase(f * 0.5))
wobble = 0.75 + 0.25 * np.sin(2 * np.pi * (7.0 - 5.0 * fall) * t)  # throb in the loudness, slowing too
dark = lowpass(drone, 2600 * (1 - fall) ** 1.6 + 160, q=1.6)
drop = (0.8 * dark + 0.6 * sub) * wobble * envelope(len(t), 0.6, 0.6, curve=1.5)
save("dome_drop.wav", drop)

# ── Dome lift ────────────────────────────────────────────────────────────────
t = t_of(3.2)
rise = np.clip(t / 3.0, 0, 1)
f = 45 * 2 ** (rise ** 0.8) * (1 + 0.025 * np.sin(2 * np.pi * (2 + 4 * rise) * t))
drone = sum(saw(f * d, 30) for d in (1.0, 1.006, 0.994)) / 3
lift = lowpass(drone, 180 + 2200 * rise ** 1.4, q=1.6) + 0.5 * np.sin(phase(f * 0.5)) * (1 - rise)
save("dome_lift.wav", lift * envelope(len(t), 0.15, 1.6, curve=1.5))

# ── Convergence ──────────────────────────────────────────────────────────────
t = t_of(3.6)
swell = np.clip(t / 3.0, 0, 1)
chord = [110.0, 138.6, 164.8, 207.7, 246.9, 329.6]   # A major with a ninth: A2 C#3 E3 G#3 B3 E4
pad = sum(saw(np.full(len(t), n) * d, 20) for n in chord for d in (1.0, 1.004)) / (len(chord) * 2)
pad = lowpass(pad, 300 + 4500 * swell ** 2, q=1.2) * envelope(len(t), 1.8, 0.6, curve=1.5)
rising = np.sin(phase(880 * 2 ** swell)) * (0.5 + 0.5 * np.sin(2 * np.pi * 9 * t)) * swell ** 3 * (t < 3.0)
converge = 0.9 * pad + 0.25 * rising
save("converge.wav", converge)
