Open Rails Scripts
==================

## TCS_France

EN :
This is a script for Open Rails that reproduces the behaviour of the French Train Control Systems.
The systems that will be available are :
- RSO : Répétition des Signaux Optique (Optical Signal Repetition)
- DAAT : Dispositif d'Arrêt Automatique des Trains (Automatic Train Stop System)
- KVB : Contrôle de vitesse par balises (Beacon-based Speed Control)
- TVM 300 and 430 : Transmission Voie Machine (Track to Machine Transmission)
- ETCS : European Train Control System (not available for now)

To install it, put the TCS_France.cs file and the INI files in the Script folder of your locomotive (create it if needed).
Put the SMS file and the WAV files in the Sound folder of your locomotive.
Using the existing INI files, you can write your own parameters file if your train is different.
In your ENG file, in the Engine section, write these parameters:
- ORTSTrainControlSystem( TCS_France.cs )
- ORTSTrainControlSystemSound( TCS_France.sms )
- ORTSTrainControlSystemParameters( name of your INI file )

Advice : In order to have a better ergonomy with the alerter, swap the SPACE and Z keys.

FR :
Ce script pour Open Rails reproduit le comportement des systèmes de sécurité des trains français.
Les systèmes qui seront disponibles sont :
- RSO : Répétition des Signaux Optique
- DAAT : Dispositif d'Arrêt Automatique des Trains
- KVB : Contrôle de vitesse par balises
- TVM 300 and 430 : Transmission Voie Machine
- ETCS : European Train Control System (non disponible pour le moment)

Pour l'installer, mettez le fichier TCS_France.cs et les fichiers INI dans le dossier Script de votre locomotive (créez le si nécessaire).
Mettez le fichier SMS et les fichiers WAV dans le dossier Sound de votre locomotive.
En utilisant les fichiers INI existants, vous pouvez créer votre propre fichier de paramètres si votre train est différent.
Dans votre fichier ENG, dans la section Engine, écrivez ces paramètres :
- ORTSTrainControlSystem( TCS_France.cs )
- ORTSTrainControlSystemSound( TCS_France.sms )
- ORTSTrainControlSystemParameters( nom de votre fichier INI )

Conseil : Pour avoir une meilleure ergonomie avec la veille automatique, intervertissez les touches ESPACE et W.

## PBL2BrakeController

EN :
This script reproduces the behaviour of the PBL2 brake controller.
To install it, put the PBL2BrakeController.cs file in the Script folder of your locomotive (create it if needed).
In your ENG file, in the Engine section, write this parameter:
- ORTSTrainBrakeController( PBL2BrakeController.cs )

FR :
Ce script pour Open Rails reproduit le comportement du robinet de freinage PBL2.
Pour l'installer, mettez le fichier PBL2BrakeController.cs dans le dossier Script de votre locomotive (créez le si nécessaire).
Dans votre fichier ENG, dans la section Engine, écrivez ce paramètre :
- ORTSTrainBrakeController( PBL2BrakeController.cs )

## SNCFCircuitBreaker

EN : This script reproduces the behaviour of the circuit breaker present in modern SNCF locomotives.
To install it, put the SNCFCircuitBreaker.cs file in the Script folder of your locomotive (create it if needed).
In your ENG file, in the Engine section, write this parameter:
- ORTSCircuitBreaker( SNCFCircuitBreaker.cs )

The default key assignments for the circuit breaker are:
- O for the circuit breaker closing order button BP(DJ)
- Shift+O for the circuit breaker closing authrorization switch Z(DJ)

FR :
Ce script pour Open Rails reproduit le comportement du disjoncteur présent dans les locomotives SNCF modernes.
Pour l'installer, mettez le fichier SNCFCircuitBreaker.cs dans le dossier Script de votre locomotive (créez le si nécessaire).
Dans votre fichier ENG, dans la section Engine, écrivez ce paramètre :
- ORTSCircuitBreaker( SNCFCircuitBreaker.cs )

Les touches pour contrôler le disjoncteur sont, par défaut :
- O pour le bouton de fermeture du disjonteur BP(DJ)
- Shift+O pour l'interrupteur d'autorisation de fermeture du disjoncteur Z(DJ)
