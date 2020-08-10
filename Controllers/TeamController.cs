using System;
using System.Collections.Generic;
using System.Linq;
using MeshBackend.Helpers;
using Google.Protobuf.WellKnownTypes;
using MeshBackend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MeshBackend.Controllers
{
    [ApiController]
    [Route("api/mesh/team")]
    [Produces("application/json")]
    public class TeamController:Controller
    {
        private readonly ILogger<TeamController> _logger;
        private readonly MeshContext _meshContext;

        public TeamController(ILogger<TeamController> logger, MeshContext meshContext)
        {
            _logger = logger;
            _meshContext = meshContext;
        }

        public class Member
        {
            public int Id { get; set; }
            public string Username { get; set; }
        }

        public class TeamProject
        {
            public int ProjectId { get; set; }
            public string ProjectName { get; set; }
            public string AdminName { get; set; }
        }

        public JsonResult CheckUsername(string username)
        {
            if (username == null || username.Length > 50)
            {
                return JsonReturn.ErrorReturn(104, "Invalid username.");
            }
            if (HttpContext.Session.GetString(username) == null)
            {
                return JsonReturn.ErrorReturn(2, "User status error.");
            }

            return null;
        }
        
        [HttpGet]
        public JsonResult QueryTeam(string username, int teamId)
        {
            var checkResult = CheckUsername(username);
            if (checkResult != null)
            {
                return checkResult;
            }
            var team = _meshContext.Teams.FirstOrDefault(t => t.Id == teamId);
            if (team != null)
            {
                var teamCooperation = _meshContext.Cooperations
                    .Where(c => c.TeamId == team.Id);
                var adminName = _meshContext.Users.First(u => u.Id == team.AdminId).Nickname;
                var members = _meshContext.Users
                    .Join(teamCooperation, u => u.Id, c => c.UserId, (u, c) =>
                        new Member()
                        {
                            Id = u.Id,
                            Username = u.Nickname
                        }).ToList();

                var project = _meshContext.Projects
                    .Where(p => p.TeamId == teamId);
                var teamProjects = _meshContext.Users
                    .Join(project, u => u.Id, p => p.AdminId, (u, p) =>
                        new TeamProject()
                        {
                            ProjectId = p.Id,
                            ProjectName = p.Name,
                            AdminName = u.Nickname
                        }).ToList();

                return Json(new
                {    
                    err_code = 0,
                    data = new
                    {
                        isSuccess = true,
                        msg = "",
                        team = new
                        {
                            teamId = team.Id,
                            teamName = team.Name,
                            createTime = team.CreatedTime,
                            adminName = adminName,
                            members = members,
                            teamProjects = teamProjects
                        }
                    }
                });
            }
            else
            {
                return JsonReturn.ErrorReturn(302, "Invalid teamId.");
            }
        }

        
        [HttpPost]
        public JsonResult CreateTeam(string username, string teamName)
        {
            var checkResult = CheckUsername(username);
            if (checkResult != null)
            {
                return checkResult;
            }

            var team = _meshContext.Teams.FirstOrDefault(t => t.Name == teamName);
            if (team != null)
            {
                return JsonReturn.ErrorReturn(301, "TeamName already exists.");
            }
            
            var createdTeam = new Team();

            var user = _meshContext.Users.First(u => u.Nickname == username);
            using (var transaction = _meshContext.Database.BeginTransaction())
            {
                try
                {
                    _meshContext.Teams.Add(new Team()
                    {
                        Name = teamName,
                        AdminId = user.Id
                    });
                    _meshContext.SaveChanges();
                    var newTeam = _meshContext.Teams.First(t => t.Name == teamName);
                    _meshContext.Cooperations.Add(new Cooperation()
                    {
                        TeamId = newTeam.Id,
                        UserId = user.Id
                    });
                    transaction.Commit();
                    createdTeam = newTeam;
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                    return JsonReturn.ErrorReturn(1, "Unexpected error.");
                }
            }

            var teamMembers = new List<Member>();
            teamMembers.Add(new Member(){Username = user.Nickname,Id = user.Id});
            return Json(new 
                {
                    err_code = 0,
                    data = new
                    {
                        isSuccess = true,
                        team = new 
                        {
                            teamId = createdTeam.Id,
                            createTime = createdTeam.CreatedTime.ToString(),
                            teamName = createdTeam.Name,
                            adminName = user.Nickname,
                            members = teamMembers
                        }
                    }
                }
            );
        }


        [HttpPost]
        [Route("invite")]
        public JsonResult InviteNewTeamMember(string username, int teamId, string inviteName)
        {
            var checkUsernameResult = CheckUsername(username);
            if (checkUsernameResult != null)
            {
                return checkUsernameResult;
            }

            if (inviteName == null || inviteName.Length > 50)
            {
                return JsonReturn.ErrorReturn(108, "Invalid inviteName");
            }
            
            var team = _meshContext.Teams.FirstOrDefault(t => t.Id == teamId);
            if (team == null)
            {
                return JsonReturn.ErrorReturn(302, "Team not exist.");
            }
            
            var user = _meshContext.Users.FirstOrDefault(u => u.Nickname == inviteName);
            if (user != null)
            {
                if (user.Id != team.AdminId)
                {
                    return JsonReturn.ErrorReturn(305, "Permission error ");
                }
                var cooperation = new Cooperation()
                {
                    TeamId = teamId,
                    UserId = user.Id
                };
                try
                {
                    _meshContext.Cooperations.Add(cooperation);
                    _meshContext.SaveChanges();
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                    return JsonReturn.ErrorReturn(1, "Unexpected error.");
                }

                return Json(new
                {
                    err_code = 0,
                    data = new
                    {
                        isSuccess = true,
                        msg = ""
                    }
                });
            }
            else
            {
                return JsonReturn.ErrorReturn(108, "Username or inviteName not exists.");
            }
        }
        
    }
}