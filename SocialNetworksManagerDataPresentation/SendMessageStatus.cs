﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetworksManager.DataPresentation
{
    public class SendMessageStatus
    {
        public String SocialNetworkName { get; set; }
        public String UserNameTo { get; set; }
        public String UserNameFrom { get; set; }
        public Boolean IsMessageSended { get; set; }
    }
}
