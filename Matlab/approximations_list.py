from enum import Enum


class Approximation(Enum):
    MinQuantile = 0
    Mean = 1
    MaxQuantile = 2


approximation_strings = ["Min quantile", "Mean", "Max quantile"]