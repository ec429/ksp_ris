Race Into Space
===============

"Bill Kerman's Race Into Space" is an asynchronous multiplayer mod for the
 Squad game "Kerbal Space Program".
It allows multiple games to share 'World First' state, awarding extra funds to
 whichever player is first to achieve various milestones.
At least, that's the idea; it's not finished yet.

Race Into Space is currently in alpha; it's had little testing, and the configs
 aren't done.  However, the basic mechanic works.

Race Into Space is developed by Edward Cree, who goes by the handle
 'soundnfury' on IRC and 'ec429' elsewhere.

Race Into Space is licensed under the MIT License.  Its source code can be
 found at <https://github.com/ec429/ksp_ris>.

Network details (rule 6 compliance):
The Race Into Space client KSP addon communicates over the network.
It will only communicate with the server specified by the user in the in-game
 UI (which defaults to localhost), and its communication consists of reports on
 the current game state, which are only sent when manually triggered by the
 user (by pressing the SYNC button), requests for the corresponding state of
 other players (triggered by the Join, Refresh and SYNC buttons), and messages
 used to Join or Leave a game on the server.  The full protocol is described in
 the file 'protocol' in the top level of the source repository, which can be
 found at <https://raw.githubusercontent.com/ec429/ksp_ris/master/protocol>.
